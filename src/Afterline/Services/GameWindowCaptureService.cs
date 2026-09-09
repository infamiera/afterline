using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

namespace Afterline.Services;

/// <summary>
/// Dedicated, read-only game-window capture. It is intentionally independent
/// from FiveM chat/DevTools capture: this service only verifies a game window,
/// copies its composed client rectangle, validates the frame, and writes it.
/// </summary>
public static class GameWindowCaptureService
{
    private const int MinimumCaptureDimension = 160;
    private const int SampleColumns = 16;
    private const int SampleRows = 10;

    public sealed record CaptureResult(string FilePath, int PixelWidth, int PixelHeight, string WindowTitle);

    public static bool IsAfterlineForeground()
    {
        IntPtr foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero) return false;
        _ = GetWindowThreadProcessId(foreground, out uint processId);
        return processId == (uint)Environment.ProcessId;
    }

    public static bool TryFindGameWindowForAfterlineCapture(out IntPtr gameWindow, out string reason)
    {
        gameWindow = IntPtr.Zero;
        reason = "Bring FiveM, GTA5, or GTAVLauncher into the game before capturing.";
        if (!IsAfterlineForeground()) return false;

        long largestArea = 0;
        IntPtr selectedWindow = IntPtr.Zero;
        _ = EnumWindows((window, parameter) =>
        {
            if (!IsWindowVisible(window) || IsIconic(window)) return true;
            _ = GetWindowThreadProcessId(window, out uint processId);
            if (processId == 0 || !IsSupportedGameProcess((int)processId)) return true;
            if (!TryGetClientBounds(window, out Rectangle bounds, out string _)) return true;

            long area = (long)bounds.Width * bounds.Height;
            if (area <= largestArea) return true;
            largestArea = area;
            selectedWindow = window;
            return true;
        }, IntPtr.Zero);

        gameWindow = selectedWindow;
        return selectedWindow != IntPtr.Zero;
    }

    /// <summary>
    /// Gets the exact game window that owned the foreground at the instant a
    /// global hotkey was received. The capture path deliberately keeps this
    /// handle instead of querying foreground again later: Steam, overlays and
    /// Windows focus transitions may occur between the key press and the copy.
    /// </summary>
    public static bool TryGetForegroundGameWindow(out IntPtr gameWindow, out string reason)
    {
        gameWindow = GetForegroundWindow();
        reason = "Bring FiveM, GTA5, or GTAVLauncher to the foreground before capturing.";
        if (gameWindow == IntPtr.Zero || !IsWindowVisible(gameWindow) || IsIconic(gameWindow))
        {
            gameWindow = IntPtr.Zero;
            return false;
        }

        if (!TryGetSupportedGameWindow(gameWindow, out _, out _, out reason))
        {
            gameWindow = IntPtr.Zero;
            return false;
        }

        return true;
    }

    public static bool ActivateGameWindow(IntPtr gameWindow)
    {
        if (gameWindow == IntPtr.Zero || !IsWindowVisible(gameWindow)) return false;
        _ = ShowWindowAsync(gameWindow, 9); // SW_RESTORE
        return SetForegroundWindow(gameWindow);
    }

    public static CaptureResult CaptureForegroundGameWindow(
        string destinationFolder,
        string format = "PNG",
        int jpegQuality = 95)
    {
        if (string.IsNullOrWhiteSpace(destinationFolder))
            throw new ArgumentException("Choose a screenshot folder first.", nameof(destinationFolder));

        if (!TryGetForegroundGameWindow(out IntPtr gameWindow, out string reason))
            throw new InvalidOperationException(reason);

        return CaptureGameWindow(gameWindow, destinationFolder, format, jpegQuality);
    }

    public static CaptureResult CaptureGameWindow(
        IntPtr gameWindow,
        string destinationFolder,
        string format = "PNG",
        int jpegQuality = 95)
    {
        if (!TryGetSupportedGameWindow(gameWindow, out Rectangle bounds, out string title, out string reason))
            throw new InvalidOperationException(reason);

        Directory.CreateDirectory(destinationFolder);
        bool useJpeg = string.Equals(format, "JPEG", StringComparison.OrdinalIgnoreCase);
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff");
        string filePath = Path.Combine(destinationFolder, $"Afterline_FiveM_{timestamp}.{(useJpeg ? "jpg" : "png")}");
        string temporary = filePath + ".writing";

        try
        {
            // GPU windows commonly return a successful but black PrintWindow
            // image. Copy the already-composed, verified client rectangle
            // instead. This never falls back to a different application or an
            // arbitrary desktop image: the bounds belong to the game window
            // that was just verified as foreground.
            using var image = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
            bool receivedFrame = false;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                using (Graphics graphics = Graphics.FromImage(image))
                {
                    graphics.CopyFromScreen(
                        bounds.Location,
                        Point.Empty,
                        bounds.Size,
                        CopyPixelOperation.SourceCopy | CopyPixelOperation.CaptureBlt);
                }

                if (HasMeaningfulPixels(image))
                {
                    receivedFrame = true;
                    break;
                }

                // A just-activated DirectX window can produce one stale black
                // desktop-composition frame. Retrying briefly is bounded and
                // occurs only when the user explicitly requests a capture.
                if (attempt < 3) Thread.Sleep(55);
            }

            if (!receivedFrame)
            {
                throw new InvalidOperationException(
                    "Windows returned a blank game frame, so Afterline did not save a false screenshot. " +
                    "Bring the game fully into view and try again.");
            }

            SaveAtomic(image, temporary, useJpeg, jpegQuality);
            File.Move(temporary, filePath, overwrite: false);
            return new CaptureResult(filePath, bounds.Width, bounds.Height, title);
        }
        catch
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch { }
            throw;
        }
    }

    internal static void RunFrameValidationSmokeTest()
    {
        using var blank = new Bitmap(32, 32, PixelFormat.Format24bppRgb);
        if (HasMeaningfulPixels(blank))
            throw new InvalidOperationException("Blank game-frame detection accepted an empty image.");

        using var gameFrame = new Bitmap(32, 32, PixelFormat.Format24bppRgb);
        // Place the test pixel on the deterministic sampling grid. This
        // verifies the same lightweight validation used for real captures.
        gameFrame.SetPixel(16, 17, Color.FromArgb(28, 61, 93));
        if (!HasMeaningfulPixels(gameFrame))
            throw new InvalidOperationException("Game-frame validation rejected visible pixels.");
    }

    private static void SaveAtomic(Bitmap image, string temporary, bool useJpeg, int jpegQuality)
    {
        if (useJpeg)
        {
            ImageCodecInfo encoder = ImageCodecInfo.GetImageEncoders()
                .First(item => item.FormatID == ImageFormat.Jpeg.Guid);
            using var parameters = new EncoderParameters(1);
            parameters.Param[0] = new EncoderParameter(
                System.Drawing.Imaging.Encoder.Quality,
                (long)Math.Clamp(jpegQuality, 70, 100));
            image.Save(temporary, encoder, parameters);
            return;
        }

        image.Save(temporary, ImageFormat.Png);
    }

    internal static bool HasMeaningfulPixels(Bitmap image)
    {
        int nonBlack = 0;
        for (int y = 0; y < SampleRows; y++)
        {
            int sampleY = Math.Min(image.Height - 1, y * Math.Max(1, image.Height - 1) / Math.Max(1, SampleRows - 1));
            for (int x = 0; x < SampleColumns; x++)
            {
                int sampleX = Math.Min(image.Width - 1, x * Math.Max(1, image.Width - 1) / Math.Max(1, SampleColumns - 1));
                Color pixel = image.GetPixel(sampleX, sampleY);
                if (pixel.R > 3 || pixel.G > 3 || pixel.B > 3)
                    nonBlack++;
            }
        }

        // A completely black PrintWindow-style frame contains no meaningful
        // samples. One visible sampled pixel is enough to retain genuinely dark
        // game scenes rather than turning this into a brightness filter.
        return nonBlack > 0;
    }

    private static bool TryGetSupportedGameWindow(
        IntPtr window,
        out Rectangle clientBounds,
        out string title,
        out string reason)
    {
        clientBounds = Rectangle.Empty;
        title = string.Empty;
        reason = "Afterline could not use that game window for capture.";

        if (window == IntPtr.Zero || !IsWindowVisible(window) || IsIconic(window))
            return false;

        _ = GetWindowThreadProcessId(window, out uint processId);
        if (processId == 0 || !IsSupportedGameProcess((int)processId))
        {
            reason = "Afterline only captures a foreground FiveM game subprocess, GTA5.exe, or GTAVLauncher.exe.";
            return false;
        }

        if (!TryGetClientBounds(window, out clientBounds, out reason)) return false;
        title = GetWindowTitle(window);
        return true;
    }

    private static bool TryGetClientBounds(IntPtr window, out Rectangle bounds, out string reason)
    {
        bounds = Rectangle.Empty;
        reason = "Afterline could not read the game window's client area.";
        if (!GetClientRect(window, out NativeRect rect) ||
            !ClientToScreen(window, ref rect.LeftTop) ||
            !ClientToScreen(window, ref rect.RightBottom))
        {
            return false;
        }

        int width = rect.RightBottom.X - rect.LeftTop.X;
        int height = rect.RightBottom.Y - rect.LeftTop.Y;
        if (width < MinimumCaptureDimension || height < MinimumCaptureDimension)
        {
            reason = "The supported game window is minimized or too small to capture.";
            return false;
        }

        bounds = new Rectangle(rect.LeftTop.X, rect.LeftTop.Y, width, height);
        reason = string.Empty;
        return true;
    }

    private static bool IsSupportedGameProcess(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            string name = process.ProcessName;
            bool expectedName = name.StartsWith("FiveM", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(name, "GTA5", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(name, "GTAVLauncher", StringComparison.OrdinalIgnoreCase);
            if (!expectedName) return false;

            try
            {
                string? executable = process.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(executable))
                {
                    string fileName = Path.GetFileNameWithoutExtension(executable);
                    return fileName.StartsWith("FiveM", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(fileName, "GTA5", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(fileName, "GTAVLauncher", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { }

            return true;
        }
        catch { return false; }
    }

    private static string GetWindowTitle(IntPtr window)
    {
        int length = GetWindowTextLength(window);
        if (length <= 0) return "FiveM game window";
        var builder = new StringBuilder(length + 1);
        _ = GetWindowText(window, builder, builder.Capacity);
        return builder.ToString();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public NativePoint LeftTop; public NativePoint RightBottom; }

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindowAsync(IntPtr hWnd, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr hWnd, out NativeRect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(IntPtr hWnd, ref NativePoint point);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);
}

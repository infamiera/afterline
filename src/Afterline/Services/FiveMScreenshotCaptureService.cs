using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

namespace Afterline.Services;

/// <summary>
/// Captures only the client area of the foreground FiveM/GTA window. There is no
/// desktop fallback: an unsupported or unfocused process is always rejected.
/// </summary>
public static class FiveMScreenshotCaptureService
{
    private const int MinimumCaptureDimension = 160;

    public sealed record CaptureResult(string FilePath, int PixelWidth, int PixelHeight, string WindowTitle);

    public static CaptureResult CaptureForegroundWindow(string destinationFolder)
    {
        if (string.IsNullOrWhiteSpace(destinationFolder))
            throw new ArgumentException("Choose a screenshot folder first.", nameof(destinationFolder));

        if (!TryGetSupportedForegroundWindow(out IntPtr window, out Rectangle bounds, out string title, out string reason))
            throw new InvalidOperationException(reason);

        Directory.CreateDirectory(destinationFolder);
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff");
        string filePath = Path.Combine(destinationFolder, $"Afterline_FiveM_{timestamp}.png");
        string temporary = filePath + ".writing";

        try
        {
            using var image = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppPArgb);
            using (Graphics graphics = Graphics.FromImage(image))
            {
                graphics.CopyFromScreen(
                    bounds.Left,
                    bounds.Top,
                    0,
                    0,
                    bounds.Size,
                    CopyPixelOperation.SourceCopy);
            }

            image.Save(temporary, ImageFormat.Png);
            File.Move(temporary, filePath, overwrite: false);
            return new CaptureResult(filePath, bounds.Width, bounds.Height, title);
        }
        catch
        {
            try
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
            catch { }
            throw;
        }
    }

    private static bool TryGetSupportedForegroundWindow(
        out IntPtr window,
        out Rectangle clientBounds,
        out string title,
        out string reason)
    {
        window = GetForegroundWindow();
        clientBounds = Rectangle.Empty;
        title = string.Empty;
        reason = "Bring FiveM, GTA5, or GTAVLauncher to the foreground before capturing.";

        if (window == IntPtr.Zero || !IsWindowVisible(window) || IsIconic(window))
            return false;

        _ = GetWindowThreadProcessId(window, out uint processId);
        if (processId == 0 || !IsSupportedProcess((int)processId))
        {
            reason = "Afterline only captures a foreground FiveM game subprocess, GTA5.exe, or GTAVLauncher.exe.";
            return false;
        }

        if (!GetClientRect(window, out NativeRect rect) ||
            !ClientToScreen(window, ref rect.LeftTop) ||
            !ClientToScreen(window, ref rect.RightBottom))
        {
            reason = "Afterline could not read the game window's client area.";
            return false;
        }

        int width = rect.RightBottom.X - rect.LeftTop.X;
        int height = rect.RightBottom.Y - rect.LeftTop.Y;
        if (width < MinimumCaptureDimension || height < MinimumCaptureDimension)
        {
            reason = "The supported game window is minimized or too small to capture.";
            return false;
        }

        clientBounds = new Rectangle(rect.LeftTop.X, rect.LeftTop.Y, width, height);
        title = GetWindowTitle(window);
        return true;
    }

    private static bool IsSupportedProcess(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            string processName = process.ProcessName;
            bool expectedName = processName.StartsWith("FiveM", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(processName, "GTA5", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(processName, "GTAVLauncher", StringComparison.OrdinalIgnoreCase);
            if (!expectedName)
                return false;

            // ProcessName is the first gate. When Windows permits it, also require
            // the executable filename to be the matching FiveM/GTA family so a
            // differently named window can never become a capture target.
            string? executable = null;
            try { executable = process.MainModule?.FileName; }
            catch { }
            if (string.IsNullOrWhiteSpace(executable))
                return expectedName;

            string fileName = Path.GetFileNameWithoutExtension(executable);
            return fileName.StartsWith("FiveM", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fileName, "GTA5", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fileName, "GTAVLauncher", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
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
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public NativePoint LeftTop;
        public NativePoint RightBottom;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

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

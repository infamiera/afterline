using System.Windows.Media.Imaging;

namespace Afterline.Services;

public static class ProfilePictureService
{
    public static BitmapImage? Load()
    {
        try
        {
            if (!File.Exists(AppPaths.ProfilePictureFile)) return null;

            byte[] bytes = File.ReadAllBytes(AppPaths.ProfilePictureFile);
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to load the local profile picture.", ex);
            return null;
        }
    }

    public static void Save(BitmapSource image)
    {
        AppPaths.EnsureLocalDirectories();
        string temp = AppPaths.ProfilePictureFile + ".tmp";

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using (var stream = File.Create(temp))
            encoder.Save(stream);

        File.Move(temp, AppPaths.ProfilePictureFile, true);
    }

    public static void Delete()
    {
        try
        {
            if (File.Exists(AppPaths.ProfilePictureFile))
                File.Delete(AppPaths.ProfilePictureFile);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to remove the local profile picture.", ex);
        }
    }
}

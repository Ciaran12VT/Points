using Points.Global;
using Points.Models;
using System.Globalization;

namespace Points.Services;

public static class UdmdImageFileStore
{
    private const string DefaultExtension = ".jpg";

    public static string CreateFileName(UdmdConfigModel config, DateTime createdAt, string? sourceFileName)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        var safeFieldName = MakeSafeFileNamePart(config.FieldName);
        var timestamp = createdAt.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
        var extension = NormalizeExtension(sourceFileName);

        return $"FieldID_{config.UdmdConfigID}_{safeFieldName}_{timestamp}{extension}";
    }

    public static string GetImagePath(long cardId, string fileName)
    {
        return Path.Combine(AppPaths.GetImageMetadataPath(cardId), fileName);
    }

    public static bool IsSafeStoredFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        if (!string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
            return false;

        return fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }

    public static bool ImageExists(long cardId, string fileName)
    {
        return IsSafeStoredFileName(fileName) && File.Exists(GetImagePath(cardId, fileName));
    }

    public static void TryDeleteFile(string? path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best effort cleanup for staged/cancelled metadata images.
        }
    }

    public static void TryDeleteCardFolder(long cardId)
    {
        try
        {
            var folder = AppPaths.GetImageMetadataPath(cardId);
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
        catch
        {
            // Best effort cleanup when a card is deleted.
        }
    }

    private static string MakeSafeFileNamePart(string? value)
    {
        var safe = string.IsNullOrWhiteSpace(value) ? "Image" : value.Trim();

        foreach (var c in Path.GetInvalidFileNameChars())
            safe = safe.Replace(c, '_');

        safe = safe.Replace(' ', '_');
        return string.IsNullOrWhiteSpace(safe) ? "Image" : safe;
    }

    private static string NormalizeExtension(string? sourceFileName)
    {
        var extension = Path.GetExtension(sourceFileName);
        if (string.IsNullOrWhiteSpace(extension))
            return DefaultExtension;

        extension = extension.ToLowerInvariant();
        return extension.Length <= 8 ? extension : DefaultExtension;
    }
}

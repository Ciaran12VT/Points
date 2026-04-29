namespace Points.Services;

public static class UdmdImageFileStore
{
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
        return IsSafeStoredFileName(fileName);
    }

    public static void TryDeleteCardFolder(long cardId)
    {
        var folder = Points.Global.AppPaths.GetImageMetadataPath(cardId);
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);
    }
}

namespace Penumbra.UI.ModsTab.ModPreview;

internal static class PreviewImageFile
{
    private const int MaxFileNameLength = 180;

    public static string WriteUnique(string folder, string preferredFileName, Action<Stream> write)
    {
        Directory.CreateDirectory(folder);
        var (path, stream) = CreateUnique(folder, preferredFileName);
        try
        {
            using (stream)
                write(stream);
            return path;
        }
        catch
        {
            stream.Dispose();
            TryDelete(path);
            throw;
        }
    }

    public static async Task<string> WriteUniqueAsync(string folder, string preferredFileName,
        Func<Stream, Task> write)
    {
        Directory.CreateDirectory(folder);
        var (path, stream) = CreateUnique(folder, preferredFileName);
        try
        {
            await using (stream)
                await write(stream);
            return path;
        }
        catch
        {
            await stream.DisposeAsync();
            TryDelete(path);
            throw;
        }
    }

    private static (string Path, FileStream Stream) CreateUnique(string folder, string preferredFileName)
    {
        var sanitized = SanitizeFileName(preferredFileName);
        var extension = Path.GetExtension(sanitized);
        var stem = Path.GetFileNameWithoutExtension(sanitized);
        if (stem.Length == 0)
            stem = "preview";

        for (var index = 0; index < 10_000; ++index)
        {
            var suffix = index == 0 ? string.Empty : $"_{index}";
            var candidate = Path.Combine(folder, $"{stem}{suffix}{extension}");
            try
            {
                var stream = new FileStream(candidate, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    81920, FileOptions.Asynchronous);
                return (candidate, stream);
            }
            catch (IOException) when (File.Exists(candidate))
            {
                // Another import already reserved this name. Try the next suffix.
            }
        }

        var fallback = Path.Combine(folder, $"{stem}_{Guid.NewGuid():N}{extension}");
        return (fallback, new FileStream(fallback, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            81920, FileOptions.Asynchronous));
    }

    private static string SanitizeFileName(string preferredFileName)
    {
        var fileName = Path.GetFileName(preferredFileName);
        foreach (var character in Path.GetInvalidFileNameChars())
            fileName = fileName.Replace(character, '_');

        fileName = fileName.Trim().TrimEnd('.', ' ');
        if (fileName.Length == 0)
            fileName = "preview";

        if (fileName.Length <= MaxFileNameLength)
            return fileName;

        var extension = Path.GetExtension(fileName);
        if (extension.Length >= MaxFileNameLength)
            extension = extension[..Math.Min(16, extension.Length)];

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var maxStemLength = Math.Max(1, MaxFileNameLength - extension.Length);
        return stem[..Math.Min(stem.Length, maxStemLength)] + extension;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Preserve the original write error.
        }
    }
}

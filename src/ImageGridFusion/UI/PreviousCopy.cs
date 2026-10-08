namespace ImageGridFusion.UI;

/// <summary>
/// The folder next to the exe keeping the last copied content — the PNG, MP4, GIF or light JPEG of
/// the last Copy — and that one only, named after the files of the grid's cells. The temp folder the
/// clipboard points at is left as it is: this is a copy.
/// See workfiles/20261008-previous-copy-folder.md.
/// </summary>
internal static class PreviousCopy
{
    public const string FolderName = "previous";

    private const string Separator = " + ";

    /// <summary>The file being written, renamed once complete: a failed write never replaces the content kept.</summary>
    private const string PartialName = "~writing";

    public static string Folder => Path.Combine(AppContext.BaseDirectory, FolderName);

    /// <summary>
    /// The kept file's name, without extension: the cells' file names without their extensions, in
    /// cell order, joined by <c> + </c> — cells without a file skipped, a name met twice kept once —
    /// or <c>fusion-yyyyMMdd-HHmmss</c> from <paramref name="now"/> when no cell has a file.
    /// </summary>
    public static string Name(IEnumerable<string?> cellFiles, DateTime now)
    {
        var names = cellFiles
            .Where(path => !string.IsNullOrEmpty(path))
            .Select(path => Path.GetFileNameWithoutExtension(path!))
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return names.Count > 0 ? string.Join(Separator, names) : $"fusion-{now:yyyyMMdd-HHmmss}";
    }

    /// <summary>Keeps a copy of <paramref name="sourcePath"/>, a file the Copy wrote, with its extension.</summary>
    public static void KeepCopyOf(string sourcePath, IEnumerable<string?> cellFiles) =>
        Keep(Path.GetExtension(sourcePath), cellFiles, path => File.Copy(sourcePath, path, overwrite: true));

    /// <summary>Keeps <paramref name="content"/> as a file with <paramref name="extension"/> (dot included).</summary>
    public static void Keep(byte[] content, string extension, IEnumerable<string?> cellFiles) =>
        Keep(extension, cellFiles, path => File.WriteAllBytes(path, content));

    /// <summary>
    /// Writes the new file, then deletes every other file of the folder — subfolders left alone, a file
    /// in use left for the next Copy; the folder made when missing. Throws an <see cref="IOException"/>
    /// or an <see cref="UnauthorizedAccessException"/> when the new file cannot be written, the folder
    /// then holding what it held.
    /// </summary>
    private static void Keep(string extension, IEnumerable<string?> cellFiles, Action<string> write)
    {
        Directory.CreateDirectory(Folder);
        string path = Path.Combine(Folder, Name(cellFiles, DateTime.Now) + extension);
        string partial = Path.Combine(Folder, PartialName + extension);
        try
        {
            write(partial);
            File.Move(partial, path, overwrite: true);
        }
        catch
        {
            TryDelete(partial);
            throw;
        }

        foreach (string file in Directory.EnumerateFiles(Folder))
        {
            if (!string.Equals(Path.GetFullPath(file), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(file);
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // In use, or protected: left for the next Copy.
        }
    }
}

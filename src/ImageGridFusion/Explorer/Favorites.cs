using System.Text;

namespace ImageGridFusion.Explorer;

/// <summary>
/// The file explorer's favorites: absolute paths, in the order they were added, kept in a text file
/// next to the exe and written at once on every change — absolute, so a favorite survives a rescan and
/// a change of base folder. See workfiles/20260926-file-explorer.md § Favorites.
/// </summary>
internal sealed class Favorites
{
    public const string FileName = "favorites.txt";

    private readonly string _path;
    private readonly List<string> _paths = [];
    private readonly HashSet<string> _set = new(StringComparer.OrdinalIgnoreCase);

    public Favorites(string path)
    {
        _path = path;
    }

    /// <summary>Where the favorites of this exe live: next to it.</summary>
    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, FileName);

    public int Count => _paths.Count;

    /// <summary>The favorites, the most recently added first.</summary>
    public IEnumerable<string> Newest
    {
        get
        {
            for (int i = _paths.Count - 1; i >= 0; i--)
            {
                yield return _paths[i];
            }
        }
    }

    public bool Contains(string fullPath) => _set.Contains(fullPath);

    /// <summary>Reads the file; a missing or unreadable one leaves no favorite.</summary>
    public void Load()
    {
        _paths.Clear();
        _set.Clear();
        string[] lines;
        try
        {
            lines = File.ReadAllLines(_path);
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
            return;
        }

        foreach (string line in lines)
        {
            if (line.Length > 0 && _set.Add(line))
            {
                _paths.Add(line);
            }
        }
    }

    /// <summary>
    /// Adds <paramref name="fullPath"/>, or removes it when it is one already, and writes the file;
    /// true when it is now a favorite. Throws an <see cref="FileIndex.IsFileError"/> exception when the
    /// file cannot be written, the change kept for the session.
    /// </summary>
    public bool Toggle(string fullPath)
    {
        bool added = !Drop(fullPath);
        if (added)
        {
            _set.Add(fullPath);
            _paths.Add(fullPath);
        }

        Save();
        return added;
    }

    /// <summary>
    /// Adds <paramref name="fullPaths"/> in their order, the last one ending the newest — one already a
    /// favorite moved there, as if added again, never removed — and writes the file once. Throws like
    /// <see cref="Toggle"/>, the change kept for the session.
    /// </summary>
    public void Add(IEnumerable<string> fullPaths)
    {
        foreach (string fullPath in fullPaths)
        {
            Drop(fullPath);
            _set.Add(fullPath);
            _paths.Add(fullPath);
        }

        Save();
    }

    /// <summary>Removes <paramref name="fullPath"/> if it is a favorite, writing the file; false when it was none.</summary>
    public bool Remove(string fullPath)
    {
        if (!Drop(fullPath))
        {
            return false;
        }

        Save();
        return true;
    }

    private bool Drop(string fullPath)
    {
        if (!_set.Remove(fullPath))
        {
            return false;
        }

        _paths.RemoveAll(p => string.Equals(p, fullPath, StringComparison.OrdinalIgnoreCase));
        return true;
    }

    private void Save()
    {
        string temp = _path + ".tmp";
        File.WriteAllLines(temp, _paths, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temp, _path, overwrite: true);
    }
}

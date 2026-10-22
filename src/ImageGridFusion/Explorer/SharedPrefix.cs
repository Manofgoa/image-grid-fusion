namespace ImageGridFusion.Explorer;

/// <summary>
/// A folder's name cut after the leading words it shares with a sibling: <see cref="Prefix"/> drawn
/// small, <see cref="Rest"/> large — <c>Outils RS - Alpha</c> → <c>Outils RS</c> and <c>Alpha</c>.
/// </summary>
internal readonly record struct SplitName(string Prefix, string Rest);

/// <summary>
/// The prefixes folder tiles show small above the rest of their name: for each folder, the longest run
/// of whole leading words it shares with another folder of the same parent, compared ignoring case,
/// leaving at least one word of its own. Words are split on spaces, <c>-</c>, <c>_</c> and <c>.</c>.
/// See workfiles/20261022-folder-tile-cleanup.md § Shared Prefix.
/// </summary>
internal static class SharedPrefix
{
    private static readonly char[] Separators = [' ', '-', '_', '.'];

    /// <summary>
    /// The split names of the <paramref name="folders"/> — full paths — sharing a prefix with a
    /// sibling, by full path; a folder sharing none is left out.
    /// </summary>
    public static Dictionary<string, SplitName> Of(IEnumerable<string> folders)
    {
        var split = new Dictionary<string, SplitName>(StringComparer.OrdinalIgnoreCase);
        var siblings = folders
            .Select(path => (Path: path, Name: Path.GetFileName(path)))
            .GroupBy(folder => Path.GetDirectoryName(folder.Path) ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        foreach (var group in siblings)
        {
            var named = group.Select(folder => (folder.Path, folder.Name, Words: Words(folder.Name))).ToList();
            if (named.Count < 2)
            {
                continue;
            }

            for (int i = 0; i < named.Count; i++)
            {
                var (path, name, words) = named[i];
                int shared = 0;
                for (int j = 0; j < named.Count; j++)
                {
                    int common = j == i ? 0 : Common(name, words, named[j].Name, named[j].Words);

                    // Shared whole, the name would keep no word of its own: that sibling gives no prefix.
                    if (common < words.Count)
                    {
                        shared = Math.Max(shared, common);
                    }
                }

                if (shared > 0)
                {
                    split[path] = new SplitName(name[..words[shared - 1].End], name[words[shared].Start..]);
                }
            }
        }

        return split;
    }

    // The words of a name, as ranges: the runs between separators.
    private static List<(int Start, int End)> Words(string name)
    {
        var words = new List<(int Start, int End)>();
        int start = -1;
        for (int i = 0; i <= name.Length; i++)
        {
            bool separator = i == name.Length || Array.IndexOf(Separators, name[i]) >= 0;
            if (separator && start >= 0)
            {
                words.Add((start, i));
                start = -1;
            }
            else if (!separator && start < 0)
            {
                start = i;
            }
        }

        return words;
    }

    // How many leading words two names share, ignoring case.
    private static int Common(string name, List<(int Start, int End)> words, string other, List<(int Start, int End)> otherWords)
    {
        int count = 0;
        while (count < words.Count && count < otherWords.Count)
        {
            var (start, end) = words[count];
            var (otherStart, otherEnd) = otherWords[count];
            if (!name.AsSpan(start, end - start).Equals(other.AsSpan(otherStart, otherEnd - otherStart), StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            count++;
        }

        return count;
    }
}

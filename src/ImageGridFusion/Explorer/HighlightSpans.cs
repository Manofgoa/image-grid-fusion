namespace ImageGridFusion.Explorer;

/// <summary>
/// Where the words of a search stand in a text, for the yellow highlight of the Details view's
/// excerpt: every occurrence of every word, anywhere inside a word of the text, accents and case
/// ignored as the search ignores them (<see cref="FileSearch.Fold"/>), the overlapping spans merged.
/// After emoji-selector's <c>EmojiSearch.MatchSpans</c>; see
/// workfiles/20261009-explorer-details-view.md § The Excerpt and Its Highlight.
/// </summary>
internal static class HighlightSpans
{
    /// <summary>The spans of <paramref name="text"/> holding one of <paramref name="foldedWords"/>, in order, never overlapping.</summary>
    public static IReadOnlyList<(int Start, int Length)> Of(string text, IReadOnlyList<string> foldedWords)
    {
        if (text.Length == 0 || foldedWords.Count == 0)
        {
            return [];
        }

        // The folded text, and for each of its characters the original one it comes from: an accent
        // dropped by the folding still belongs to the letter it was on.
        var (folded, origin) = Folded(text);
        var spans = new List<(int Start, int End)>();
        foreach (string word in foldedWords)
        {
            if (word.Length == 0)
            {
                continue;
            }

            for (int at = folded.IndexOf(word, StringComparison.Ordinal); at >= 0; at = folded.IndexOf(word, at + 1, StringComparison.Ordinal))
            {
                int start = origin[at];
                int end = at + word.Length < folded.Length ? origin[at + word.Length] : text.Length;
                spans.Add((start, end));
            }

            // Letters the OCR read apart, as the search finds them: the spaces removed, the span running
            // from the first letter to the last (workfiles/20261013-complementary-ocr-passes.md § Spaces-Ignored Match).
            if (word.Length >= FileSearch.JoinedMinLength)
            {
                var (joined, from) = Joined(folded, origin);
                for (int at = joined.IndexOf(word, StringComparison.Ordinal); at >= 0; at = joined.IndexOf(word, at + 1, StringComparison.Ordinal))
                {
                    int last = from[at + word.Length - 1];
                    spans.Add((from[at], last + (char.IsSurrogatePair(text, last) ? 2 : 1)));
                }
            }
        }

        spans.Sort((a, b) => a.Start.CompareTo(b.Start));
        var merged = new List<(int Start, int Length)>();
        foreach (var (start, end) in spans)
        {
            if (merged.Count > 0 && start <= merged[^1].Start + merged[^1].Length)
            {
                var last = merged[^1];
                merged[^1] = (last.Start, Math.Max(last.Length, end - last.Start));
            }
            else
            {
                merged.Add((start, end - start));
            }
        }

        return merged;
    }

    /// <summary>The folded text without its white space, with where each of its characters comes from in the original text.</summary>
    private static (string Joined, int[] Origin) Joined(string folded, int[] origin)
    {
        var joined = new System.Text.StringBuilder(folded.Length);
        var from = new List<int>(folded.Length);
        for (int i = 0; i < folded.Length; i++)
        {
            if (!char.IsWhiteSpace(folded[i]))
            {
                joined.Append(folded[i]);
                from.Add(origin[i]);
            }
        }

        return (joined.ToString(), [.. from]);
    }

    /// <summary>The text folded one character at a time — a surrogate pair kept whole — with where each folded character comes from.</summary>
    private static (string Folded, int[] Origin) Folded(string text)
    {
        var folded = new System.Text.StringBuilder(text.Length);
        var origin = new List<int>(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            int length = char.IsSurrogatePair(text, i) ? 2 : 1;
            string part = char.IsSurrogate(text[i]) ? text.Substring(i, length) : FileSearch.Fold(text.Substring(i, length));
            folded.Append(part);
            for (int j = 0; j < part.Length; j++)
            {
                origin.Add(i);
            }

            i += length - 1;
        }

        return (folded.ToString(), [.. origin]);
    }
}

using System.Security.Cryptography;
using System.Text;

namespace ImageGridFusion.Explorer;

/// <summary>
/// The tags of the images, computed from their embeddings — never by a model of their own: a bilingual
/// vocabulary of concepts — the one shipped (<c>VisionTags.txt</c>, embedded) and the user's additions
/// (<see cref="UserFileName"/> next to the exe) — each concept's text embedding computed once and cached in
/// <see cref="CacheFileName"/>, compared with an image's: the closest concepts are its tags, their French
/// and English words written into the Tags slot of the content texts. A vocabulary change gives another
/// <see cref="Key"/>, and the tags are computed again from the stored embeddings. See
/// workfiles/20261027-image-content-analysis.md § Tags.
/// </summary>
internal sealed class VisionTags
{
    /// <summary>The user's additions to the vocabulary, next to the exe.</summary>
    public const string UserFileName = "vision-tags.txt";

    /// <summary>The concepts' embeddings, in the indexing folder.</summary>
    public const string CacheFileName = "vision-concepts.bin";

    // A concept is a tag of an image when its cosine reaches the floor and comes within the spread of the
    // image's best one; at most MaxTags, the best first. Tuned on sample images (Iteration 8).
    private const float Floor = 0.105f;
    private const float Spread = 0.02f;
    private const int MaxTags = 6;

    // Changes when the rule above or the slot's format changes, so the stored tags are computed again.
    private const string RuleVersion = "2";

    /// <summary>What separates the concepts in the Tags slot — the tile's tooltip shows them apart; the search ignores it.</summary>
    public const string ConceptSeparator = " · ";

    private const string CacheMagic = "ImageGridFusion vision concepts";
    private const string ResourceName = "ImageGridFusion.Explorer.VisionTags.txt";

    private const string UserFileTemplate =
        "# Image Grid Fusion — your own tags, added to the ones shipped with the app.\n"
        + "# One concept a line: its French words | its English words [| the prompt it is embedded with].\n"
        + "# Several words of a language are separated by commas; the first English word makes the prompt\n"
        + "# \"a photo of <word>.\" unless a prompt is given. A line starting with # is a comment.\n"
        + "# Example:\n"
        + "# tour de pise | leaning tower of pisa\n";

    private readonly Concept[] _concepts;

    private VisionTags(Concept[] concepts)
    {
        this._concepts = concepts;
        var key = new StringBuilder(RuleVersion).Append('\n').Append(VisionEngineFiles.EmbeddingModelId);
        foreach (var concept in concepts)
        {
            key.Append('\n').Append(concept.Words).Append('|').Append(concept.Prompt);
        }

        this.Key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key.ToString())))[..16];
    }

    /// <summary>The user's file: <c>vision-tags.txt</c> next to the exe.</summary>
    public static string UserFile => Path.Combine(AppContext.BaseDirectory, UserFileName);

    public static string CachePath => Path.Combine(FileIndex.Folder, CacheFileName);

    /// <summary>Identifies the vocabulary and the rule: the stored tags were computed with another one when it differs.</summary>
    public string Key { get; }

    /// <summary>How many concepts the vocabulary holds.</summary>
    public int Count => this._concepts.Length;

    /// <summary>Whether every concept has its embedding.</summary>
    public bool Embedded => this._concepts.All(c => c.Embedding is not null);

    /// <summary>The vocabulary shipped, then the user's additions — a line it cannot read skipped, a missing file none.</summary>
    public static VisionTags Load()
    {
        var concepts = new List<Concept>();
        using (var stream = typeof(VisionTags).Assembly.GetManifestResourceStream(ResourceName))
        using (var reader = new StreamReader(stream!, Encoding.UTF8))
        {
            concepts.AddRange(Parse(reader.ReadToEnd()));
        }

        try
        {
            if (File.Exists(UserFile))
            {
                concepts.AddRange(Parse(File.ReadAllText(UserFile, Encoding.UTF8)));
            }
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
        }

        return new VisionTags(concepts.ToArray());
    }

    /// <summary>Creates the user's file with its explanation when missing — a user action turning the analysis on. Throws like <see cref="File.WriteAllText(string, string?)"/>.</summary>
    public static void CreateUserFile()
    {
        if (!File.Exists(UserFile))
        {
            File.WriteAllText(UserFile, UserFileTemplate, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }

    /// <summary>
    /// Gives every concept its embedding: from the cache when it holds its prompt, else computed by
    /// <paramref name="text"/> — the text model's — the cache written again when any was. Stops when cancelled.
    /// </summary>
    public void Embed(Func<string, float[]> text, CancellationToken cancellation)
    {
        var cached = ReadCache();
        bool computed = false;
        foreach (var concept in this._concepts)
        {
            cancellation.ThrowIfCancellationRequested();
            if (concept.Embedding is not null)
            {
                continue;
            }

            if (cached.TryGetValue(concept.Prompt, out var embedding))
            {
                concept.Embedding = embedding;
            }
            else
            {
                concept.Embedding = text(concept.Prompt);
                cached[concept.Prompt] = concept.Embedding;
                computed = true;
            }
        }

        if (computed)
        {
            WriteCache(cached);
        }
    }

    /// <summary>The tags of an image, its French and English words concept by concept (<see cref="ConceptSeparator"/>), the closest concept first; "" for none. Every concept embedded first.</summary>
    public string TagsOf(float[] embedding)
    {
        var scored = this._concepts.Where(c => c.Embedding is not null)
            .Select(c => (Concept: c, Score: Embeddings.Dot(c.Embedding, embedding)))
            .OrderByDescending(s => s.Score)
            .ToArray();
        if (scored.Length == 0)
        {
            return "";
        }

        float least = Math.Max(Floor, scored[0].Score - Spread);
        return string.Join(ConceptSeparator, scored.TakeWhile(s => s.Score >= least).Take(MaxTags).Select(s => s.Concept.Words));
    }

    /// <summary>Deletes the concepts' cache; a missing one is no error. Throws like <see cref="File.Delete"/>.</summary>
    public static void DeleteCache()
    {
        if (File.Exists(CachePath))
        {
            File.Delete(CachePath);
        }
    }

    private static IEnumerable<Concept> Parse(string text)
    {
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            string[] parts = line.Split('|');
            if (parts.Length < 2)
            {
                continue;
            }

            string[] french = Words(parts[0]);
            string[] english = Words(parts[1]);
            if (english.Length == 0)
            {
                continue;
            }

            string prompt = parts.Length > 2 && parts[2].Trim() is { Length: > 0 } given ? given : $"a photo of {english[0]}.";
            yield return new Concept(string.Join(' ', french.Concat(english)), prompt);
        }

        static string[] Words(string part) => part.Split(',').Select(w => w.Trim()).Where(w => w.Length > 0).ToArray();
    }

    private static Dictionary<string, float[]> ReadCache()
    {
        var cached = new Dictionary<string, float[]>(StringComparer.Ordinal);
        try
        {
            using var reader = new BinaryReader(File.OpenRead(CachePath), Encoding.UTF8);
            if (reader.ReadString() != CacheMagic || reader.ReadString() != VisionEngineFiles.EmbeddingModelId)
            {
                return cached;
            }

            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                string prompt = reader.ReadString();
                var embedding = new float[reader.ReadInt32()];
                for (int v = 0; v < embedding.Length; v++)
                {
                    embedding[v] = reader.ReadSingle();
                }

                cached[prompt] = embedding;
            }
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex) || ex is EndOfStreamException or FormatException or OverflowException or ArgumentException)
        {
        }

        return cached;
    }

    private static void WriteCache(Dictionary<string, float[]> cached)
    {
        try
        {
            Directory.CreateDirectory(FileIndex.Folder);
            string temp = CachePath + ".tmp";
            using (var writer = new BinaryWriter(File.Create(temp), Encoding.UTF8))
            {
                writer.Write(CacheMagic);
                writer.Write(VisionEngineFiles.EmbeddingModelId);
                writer.Write(cached.Count);
                foreach (var (prompt, embedding) in cached)
                {
                    writer.Write(prompt);
                    writer.Write(embedding.Length);
                    foreach (float value in embedding)
                    {
                        writer.Write(value);
                    }
                }
            }

            File.Move(temp, CachePath, overwrite: true);
        }
        catch (Exception ex) when (FileIndex.IsFileError(ex))
        {
            // Computed again next time.
        }
    }

    /// <summary>A concept: its words, both languages, as the Tags slot writes them; the prompt it is embedded with; its embedding once known.</summary>
    private sealed class Concept(string words, string prompt)
    {
        public string Words { get; } = words;

        public string Prompt { get; } = prompt;

        public float[]? Embedding { get; set; }
    }
}

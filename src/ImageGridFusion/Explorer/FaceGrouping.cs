namespace ImageGridFusion.Explorer;

/// <summary>A face of an indexed file: the file's full path, the face, and when the file was created — the ungrouped faces' order.</summary>
internal sealed record FaceRef(string FullPath, FaceBox Face, DateTime Created)
{
    private string? _key;

    public string Key => this._key ??= FaceKeys.Of(this.FullPath, this.Face.Bounds);

    private float _quality = -1;

    /// <summary>How good a picture of the person it makes (<see cref="FaceQuality"/>).</summary>
    public float Quality => this._quality >= 0 ? this._quality : this._quality = FaceQuality.Of(this.Face);
}

/// <summary>
/// A face group of the People view: a person the user acted on (<see cref="PersonId"/>), or faces the
/// grouping found close — unnamed, its id taken from its first face. A group merged into another is
/// one of its <see cref="SubGroups"/>, one level deep.
/// </summary>
internal sealed class FaceGroup(string id, string? personId, string name, bool hidden)
{
    private FaceRef? _best;
    private int _fileCount = -1;

    public string Id { get; } = id;

    /// <summary>The person behind it; null for a group only the grouping made.</summary>
    public string? PersonId { get; } = personId;

    /// <summary>The name the user gave; "" for none.</summary>
    public string Name { get; } = name;

    public bool Hidden { get; } = hidden;

    /// <summary>Its own faces, its sub-groups' aside.</summary>
    public List<FaceRef> Faces { get; } = [];

    public List<FaceGroup> SubGroups { get; } = [];

    /// <summary>The group it was merged into; null for a group of its own.</summary>
    public FaceGroup? Parent { get; set; }

    /// <summary>Its faces and its sub-groups'.</summary>
    public IEnumerable<FaceRef> AllFaces => this.Faces.Concat(this.SubGroups.SelectMany(group => group.Faces));

    /// <summary>Its best face — the best quality, the largest of those — the group's picture.</summary>
    public FaceRef Best => this._best ??= this.AllFaces.MaxBy(face => (face.Quality, face.Face.Side))!;

    /// <summary>How many files its faces and its sub-groups' are in.</summary>
    public int FileCount => this._fileCount >= 0 ? this._fileCount
        : this._fileCount = this.AllFaces.Select(face => face.FullPath).Distinct(StringComparer.OrdinalIgnoreCase).Count();
}

/// <summary>
/// What the People view shows, computed once per change of the faces, of the people or of the grouping's
/// strictness: the groups — visible, and hidden — then the faces close to no other, the clear ones alone
/// and the poor ones apart; every group by its id, and the group each face belongs to.
/// </summary>
internal sealed class PeopleSnapshot(IReadOnlyList<FaceGroup> groups, IReadOnlyList<FaceGroup> hidden, IReadOnlyList<FaceRef> ungrouped, IReadOnlyList<FaceRef> lowQuality)
{
    public static readonly PeopleSnapshot Empty = new([], [], [], []);

    public IReadOnlyList<FaceGroup> Groups { get; } = groups;

    public IReadOnlyList<FaceGroup> Hidden { get; } = hidden;

    /// <summary>The clear faces close to no other: the faces alone.</summary>
    public IReadOnlyList<FaceRef> Ungrouped { get; } = ungrouped;

    /// <summary>The faces close to no other under <see cref="FaceQuality.LowFloor"/>: the low-quality faces.</summary>
    public IReadOnlyList<FaceRef> LowQuality { get; } = lowQuality;

    public Dictionary<string, FaceGroup> ById { get; } = groups.Concat(hidden).SelectMany(group => group.SubGroups.Prepend(group)).ToDictionary(group => group.Id);

    /// <summary>The group — a sub-group itself, for a face of one — each grouped face belongs to.</summary>
    public Dictionary<string, FaceGroup> OwnerOf { get; } = Owners(groups.Concat(hidden));

    /// <summary>Every face by its key, grouped or not.</summary>
    public Dictionary<string, FaceRef> FaceByKey { get; } = groups.Concat(hidden).SelectMany(group => group.AllFaces).Concat(ungrouped).Concat(lowQuality)
        .DistinctBy(face => face.Key, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(face => face.Key, StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, FaceGroup> Owners(IEnumerable<FaceGroup> groups)
    {
        var owners = new Dictionary<string, FaceGroup>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups.SelectMany(group => group.SubGroups.Prepend(group)))
        {
            foreach (var face in group.Faces)
            {
                owners.TryAdd(face.Key, group);
            }
        }

        return owners;
    }
}

/// <summary>
/// Groups the faces into people (workfiles/20261027-face-detection.md § Grouping, made stricter by
/// workfiles/20261028-face-grouping-accuracy.md § Grouping). Similarities are averages — a face against a
/// group is the mean of its similarities to the group's faces, two groups the mean over every pair of their
/// faces — each face weighed by its <see cref="FaceQuality"/>, so they compare honestly to a pairwise
/// threshold; a face needs more the poorer it is (<see cref="QualityMargin"/>). A face pinned to a person
/// stays with it; any other joins the person closest to it when it reaches the threshold and is not
/// excluded from that person, else the closest seed above a stricter threshold (<see cref="SeedMargin"/>),
/// else starts one — the clearest faces first; the seeds are then merged two by two, the closest pair
/// first, while a pair reaches the threshold — the poorer side's margin included: average linkage, which
/// never unites two groups through a third. A group of a single face is no group: that face is listed
/// alone, or apart when poor.
/// </summary>
internal static class FaceGrouping
{
    /// <summary>
    /// The average cosine similarity from which faces are the same person, per strictness level — Very
    /// loose to Very strict, an app setting of the ⚙ menu (<c>AppSettings.FaceGrouping</c>). Set on real
    /// faces (workfiles/20261028-face-grouping-accuracy.md § Calibration).
    /// </summary>
    public static readonly float[] Thresholds = [0.28f, 0.32f, 0.36f, 0.40f, 0.44f];

    /// <summary>The level of <see cref="Thresholds"/> at first: Normal.</summary>
    public const int DefaultLevel = 2;

    /// <summary>What a face needs above the threshold to join a seed: seeds are tight, so the faces' order matters little.</summary>
    public const float SeedMargin = 0.08f;

    /// <summary>What the poorest face needs above the threshold, a perfect one nothing, linear between.</summary>
    public const float QualityMargin = 0.06f;

    /// <summary>The threshold of a strictness level, the default one for a level out of range.</summary>
    public static float ThresholdOf(int level) => Thresholds[level >= 0 && level < Thresholds.Length ? level : DefaultLevel];

    public static PeopleSnapshot Compute(IReadOnlyList<FaceRef> faces, IReadOnlyList<PersonData> people, float threshold)
    {
        var pinnedTo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var pinned = new List<(PersonData Person, Cluster Faces)>();
        foreach (var person in people)
        {
            foreach (string key in person.Pinned.Keys)
            {
                pinnedTo[key] = person.Id;
            }

            if (Cluster.Of(person.Pinned.Values) is { } cluster)
            {
                pinned.Add((person, cluster));
            }
        }

        var ofPerson = people.ToDictionary(person => person.Id, _ => new List<FaceRef>());
        var seeds = new List<Cluster>();
        var ordered = faces.OrderByDescending(face => face.Quality).ThenBy(face => face.Key, StringComparer.OrdinalIgnoreCase);
        foreach (var face in ordered)
        {
            if (pinnedTo.TryGetValue(face.Key, out string? id))
            {
                ofPerson[id].Add(face);
                continue;
            }

            var embedding = face.Face.Embedding;
            float needed = threshold + QualityMargin * (1 - face.Quality);
            PersonData? closest = null;
            float best = needed;
            foreach (var (person, cluster) in pinned)
            {
                float similarity = cluster.SimilarityTo(embedding);
                if (similarity >= best && !person.Excluded.Contains(face.Key))
                {
                    best = similarity;
                    closest = person;
                }
            }

            if (closest is not null)
            {
                ofPerson[closest.Id].Add(face);
                continue;
            }

            Cluster? near = null;
            best = needed + SeedMargin;
            foreach (var seed in seeds)
            {
                float similarity = seed.SimilarityTo(embedding);
                if (similarity >= best)
                {
                    best = similarity;
                    near = seed;
                }
            }

            if (near is null)
            {
                seeds.Add(new Cluster(face));
            }
            else
            {
                near.Add(face);
            }
        }

        var clusters = MergeAverage(seeds, threshold);

        // The people's groups, each merged one under the group it was merged into, at the top of its chain.
        var byId = people.ToDictionary(person => person.Id);
        var groups = people.ToDictionary(person => person.Id, person => new FaceGroup(person.Id, person.Id, person.Name, person.Hidden));
        foreach (var person in people)
        {
            groups[person.Id].Faces.AddRange(ofPerson[person.Id]);
        }

        var top = new List<FaceGroup>();
        foreach (var person in people)
        {
            string? root = RootOf(person, byId);
            if (root is null)
            {
                top.Add(groups[person.Id]);
            }
            else
            {
                groups[person.Id].Parent = groups[root];
                groups[root].SubGroups.Add(groups[person.Id]);
            }
        }

        foreach (var group in top)
        {
            group.SubGroups.RemoveAll(sub => sub.Faces.Count == 0);
        }

        top.RemoveAll(group => !group.AllFaces.Any());
        var ungrouped = new List<FaceRef>();
        var lowQuality = new List<FaceRef>();
        foreach (var cluster in clusters)
        {
            if (cluster.Faces.Count < 2)
            {
                foreach (var face in cluster.Faces)
                {
                    (face.Quality < FaceQuality.LowFloor ? lowQuality : ungrouped).Add(face);
                }

                continue;
            }

            var group = new FaceGroup("auto:" + cluster.Faces[0].Key, null, "", false);
            group.Faces.AddRange(cluster.Faces);
            top.Add(group);
        }

        var visible = Ordered(top.Where(group => !group.Hidden));
        var hidden = Ordered(top.Where(group => group.Hidden));
        foreach (var group in top)
        {
            group.SubGroups.Sort((a, b) => Compare(a, b));
        }

        ungrouped.Sort((a, b) => b.Created.CompareTo(a.Created));
        lowQuality.Sort((a, b) => b.Created.CompareTo(a.Created));
        return new PeopleSnapshot(visible, hidden, ungrouped, lowQuality);
    }

    /// <summary>The named groups A→Z first, then the unnamed ones, the larger first.</summary>
    private static List<FaceGroup> Ordered(IEnumerable<FaceGroup> groups)
    {
        var list = groups.ToList();
        list.Sort((a, b) => Compare(a, b));
        return list;
    }

    private static int Compare(FaceGroup a, FaceGroup b)
    {
        bool aNamed = a.Name.Length > 0;
        bool bNamed = b.Name.Length > 0;
        if (aNamed != bNamed)
        {
            return aNamed ? -1 : 1;
        }

        int order = aNamed ? StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name) : b.FileCount.CompareTo(a.FileCount);
        return order != 0 ? order : string.CompareOrdinal(a.Id, b.Id);
    }

    /// <summary>The top of the chain of merges a person is in; null for one merged into none — a cycle or a person gone counting as none.</summary>
    private static string? RootOf(PersonData person, Dictionary<string, PersonData> byId)
    {
        string? root = null;
        var seen = new HashSet<string> { person.Id };
        for (var at = person; at.MergedInto is { } next && byId.TryGetValue(next, out var parent) && seen.Add(next); at = parent)
        {
            root = next;
        }

        return root;
    }

    /// <summary>
    /// The seeds merged two by two, the closest pair on average first, while a pair reaches the threshold:
    /// average linkage. The poorer side of a pair — its best face — needs the quality margin as a face does,
    /// so a poor face alone in its seed joins no group it would not have joined as a face. Only the pairs
    /// within reach are queued; a merge queues the merged cluster's pairs again, the older ones dropped.
    /// </summary>
    private static List<Cluster> MergeAverage(List<Cluster> seeds, float threshold)
    {
        var alive = new bool[seeds.Count];
        Array.Fill(alive, true);
        var version = new int[seeds.Count];
        var pairs = new PriorityQueue<(int A, int B, int VersionA, int VersionB), float>();
        for (int a = 0; a < seeds.Count; a++)
        {
            for (int b = a + 1; b < seeds.Count; b++)
            {
                Offer(a, b);
            }
        }

        while (pairs.TryDequeue(out var pair, out _))
        {
            if (!alive[pair.A] || !alive[pair.B] || version[pair.A] != pair.VersionA || version[pair.B] != pair.VersionB)
            {
                continue;
            }

            seeds[pair.A].Merge(seeds[pair.B]);
            alive[pair.B] = false;
            version[pair.A]++;
            for (int other = 0; other < seeds.Count; other++)
            {
                if (other != pair.A && alive[other])
                {
                    Offer(pair.A, other);
                }
            }
        }

        return [.. seeds.Where((_, i) => alive[i])];

        void Offer(int a, int b)
        {
            float score = seeds[a].SimilarityTo(seeds[b]) - QualityMargin * (1 - Math.Min(seeds[a].Quality, seeds[b].Quality));
            if (score >= threshold)
            {
                pairs.Enqueue((a, b, version[a], version[b]), -score);
            }
        }
    }

    /// <summary>
    /// Faces found close, and the sum of their embeddings each weighed by its quality, with the total weight:
    /// what an average similarity to them takes, one dot product.
    /// </summary>
    private sealed class Cluster
    {
        private readonly float[] _sum;
        private float _weight;

        public Cluster(FaceRef first)
        {
            this._sum = new float[first.Face.Embedding.Length];
            this.Add(first);
        }

        private Cluster(int length)
        {
            this._sum = new float[length];
        }

        public List<FaceRef> Faces { get; } = [];

        /// <summary>Its best face's quality: a cluster holding a clear face needs no margin.</summary>
        public float Quality { get; private set; }

        /// <summary>A person's pinned faces, each of weight 1 — the user put them there; null for none.</summary>
        public static Cluster? Of(IEnumerable<float[]> embeddings)
        {
            Cluster? cluster = null;
            foreach (var embedding in embeddings)
            {
                cluster ??= new Cluster(embedding.Length) { Quality = 1 };
                cluster.Accumulate(embedding, 1);
            }

            return cluster is { _weight: > 0 } ? cluster : null;
        }

        public void Add(FaceRef face)
        {
            this.Faces.Add(face);
            this.Quality = Math.Max(this.Quality, face.Quality);
            this.Accumulate(face.Face.Embedding, face.Quality);
        }

        public void Merge(Cluster other)
        {
            this.Faces.AddRange(other.Faces);
            this.Quality = Math.Max(this.Quality, other.Quality);
            for (int i = 0; i < this._sum.Length; i++)
            {
                this._sum[i] += other._sum[i];
            }

            this._weight += other._weight;
        }

        /// <summary>The mean of an embedding's similarities to its faces, weighed.</summary>
        public float SimilarityTo(float[] embedding) =>
            embedding.Length == this._sum.Length ? FaceAnalyzer.Similarity(embedding, this._sum) / this._weight : float.NegativeInfinity;

        /// <summary>The mean of the similarities over every pair of their faces, weighed.</summary>
        public float SimilarityTo(Cluster other) => FaceAnalyzer.Similarity(this._sum, other._sum) / (this._weight * other._weight);

        private void Accumulate(float[] embedding, float weight)
        {
            if (embedding.Length != this._sum.Length)
            {
                return;
            }

            for (int i = 0; i < this._sum.Length; i++)
            {
                this._sum[i] += weight * embedding[i];
            }

            this._weight += weight;
        }
    }
}

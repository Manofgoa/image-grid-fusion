namespace ImageGridFusion.Explorer;

/// <summary>A face of an indexed file: the file's full path, the face, and when the file was created — the ungrouped faces' order.</summary>
internal sealed record FaceRef(string FullPath, FaceBox Face, DateTime Created)
{
    private string? _key;

    public string Key => this._key ??= FaceKeys.Of(this.FullPath, this.Face.Bounds);

    /// <summary>How good a picture of the person it makes: its size times its score.</summary>
    public float Quality => this.Face.Bounds.Width * this.Face.Bounds.Height * this.Face.Score;
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

    /// <summary>Its best face — the largest and surest — the group's picture.</summary>
    public FaceRef Best => this._best ??= this.AllFaces.MaxBy(face => face.Quality)!;

    /// <summary>How many files its faces and its sub-groups' are in.</summary>
    public int FileCount => this._fileCount >= 0 ? this._fileCount
        : this._fileCount = this.AllFaces.Select(face => face.FullPath).Distinct(StringComparer.OrdinalIgnoreCase).Count();
}

/// <summary>
/// What the People view shows, computed once per change of the faces or of the people: the groups —
/// visible, and hidden — then the faces close to no other; every group by its id, and the group each face
/// belongs to.
/// </summary>
internal sealed class PeopleSnapshot(IReadOnlyList<FaceGroup> groups, IReadOnlyList<FaceGroup> hidden, IReadOnlyList<FaceRef> ungrouped)
{
    public static readonly PeopleSnapshot Empty = new([], [], []);

    public IReadOnlyList<FaceGroup> Groups { get; } = groups;

    public IReadOnlyList<FaceGroup> Hidden { get; } = hidden;

    public IReadOnlyList<FaceRef> Ungrouped { get; } = ungrouped;

    public Dictionary<string, FaceGroup> ById { get; } = groups.Concat(hidden).SelectMany(group => group.SubGroups.Prepend(group)).ToDictionary(group => group.Id);

    /// <summary>The group — a sub-group itself, for a face of one — each grouped face belongs to.</summary>
    public Dictionary<string, FaceGroup> OwnerOf { get; } = Owners(groups.Concat(hidden));

    /// <summary>Every face by its key, grouped or not.</summary>
    public Dictionary<string, FaceRef> FaceByKey { get; } = groups.Concat(hidden).SelectMany(group => group.AllFaces).Concat(ungrouped)
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
/// Groups the faces into people (workfiles/20261027-face-detection.md § Grouping): a face pinned to a
/// person stays with it; any other joins the person closest to it — the mean of its pinned faces — when
/// its similarity reaches <see cref="SameThreshold"/> and it is not excluded from that person, else the
/// closest group of the others, else starts one; the groups so found then merged when their means are
/// that close. A group of a single face is no group: that face is listed alone.
/// </summary>
internal static class FaceGrouping
{
    /// <summary>
    /// The cosine similarity from which two faces are the same person: SFace's published value. A fixed
    /// constant — merging and removing a face correct its mistakes.
    /// </summary>
    public const float SameThreshold = 0.363f;

    public static PeopleSnapshot Compute(IReadOnlyList<FaceRef> faces, IReadOnlyList<PersonData> people)
    {
        var pinnedTo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var means = new List<(PersonData Person, float[] Mean)>();
        foreach (var person in people)
        {
            foreach (string key in person.Pinned.Keys)
            {
                pinnedTo[key] = person.Id;
            }

            if (Mean(person.Pinned.Values) is { } mean)
            {
                means.Add((person, mean));
            }
        }

        var ofPerson = people.ToDictionary(person => person.Id, _ => new List<FaceRef>());
        var clusters = new List<Cluster>();
        foreach (var face in faces.OrderBy(face => face.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (pinnedTo.TryGetValue(face.Key, out string? id))
            {
                ofPerson[id].Add(face);
                continue;
            }

            var embedding = face.Face.Embedding;
            PersonData? closest = null;
            float best = SameThreshold;
            foreach (var (person, mean) in means)
            {
                float similarity = FaceAnalyzer.Similarity(embedding, mean);
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
            best = SameThreshold;
            foreach (var cluster in clusters)
            {
                float similarity = FaceAnalyzer.Similarity(embedding, cluster.Mean);
                if (similarity >= best)
                {
                    best = similarity;
                    near = cluster;
                }
            }

            if (near is null)
            {
                clusters.Add(new Cluster(face));
            }
            else
            {
                near.Add(face);
            }
        }

        clusters = MergeClose(clusters);

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
        foreach (var cluster in clusters)
        {
            if (cluster.Faces.Count < 2)
            {
                ungrouped.AddRange(cluster.Faces);
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
        return new PeopleSnapshot(visible, hidden, ungrouped);
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

    /// <summary>The clusters whose means reach the threshold put together, transitively.</summary>
    private static List<Cluster> MergeClose(List<Cluster> clusters)
    {
        var parent = Enumerable.Range(0, clusters.Count).ToArray();
        int Find(int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }

            return i;
        }

        for (int i = 0; i < clusters.Count; i++)
        {
            for (int j = i + 1; j < clusters.Count; j++)
            {
                if (FaceAnalyzer.Similarity(clusters[i].Mean, clusters[j].Mean) >= SameThreshold)
                {
                    parent[Find(j)] = Find(i);
                }
            }
        }

        var merged = new Dictionary<int, Cluster>();
        for (int i = 0; i < clusters.Count; i++)
        {
            int root = Find(i);
            if (merged.TryGetValue(root, out var into))
            {
                foreach (var face in clusters[i].Faces)
                {
                    into.Add(face);
                }
            }
            else
            {
                merged[root] = clusters[i];
            }
        }

        return [.. merged.Values];
    }

    /// <summary>The mean of embeddings, scaled to length 1; null for none.</summary>
    private static float[]? Mean(IEnumerable<float[]> embeddings)
    {
        float[]? sum = null;
        foreach (var embedding in embeddings)
        {
            sum ??= new float[embedding.Length];
            if (embedding.Length != sum.Length)
            {
                continue;
            }

            for (int i = 0; i < sum.Length; i++)
            {
                sum[i] += embedding[i];
            }
        }

        return sum is null ? null : FaceAnalyzer.Normalize(sum);
    }

    /// <summary>Faces found close, and the mean of their embeddings, kept up to date.</summary>
    private sealed class Cluster
    {
        private readonly float[] _sum;

        public Cluster(FaceRef first)
        {
            this._sum = (float[])first.Face.Embedding.Clone();
            this.Mean = FaceAnalyzer.Normalize((float[])this._sum.Clone());
            this.Faces.Add(first);
        }

        public List<FaceRef> Faces { get; } = [];

        public float[] Mean { get; private set; }

        public void Add(FaceRef face)
        {
            this.Faces.Add(face);
            var embedding = face.Face.Embedding;
            for (int i = 0; i < this._sum.Length && i < embedding.Length; i++)
            {
                this._sum[i] += embedding[i];
            }

            this.Mean = FaceAnalyzer.Normalize((float[])this._sum.Clone());
        }
    }
}

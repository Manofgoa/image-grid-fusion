namespace ImageGridFusion.Explorer;

/// <summary>
/// How far an image found by its similarity stands out, and from where it is kept: the cosines of SigLIP 2's
/// embeddings are high for every pair of images and low for every text, so a result is read against the
/// query's own scores over the index — <c>z = (score − mean) / spread</c> — and, for a text, against the
/// image's own scores for the vocabulary's concepts, which tells an image matching this text from one
/// matching any text. See workfiles/20261029-similar-images-accuracy.md.
/// </summary>
internal static class VisionSimilarity
{
    /// <summary>
    /// How far a result must stand out, per strictness level — Very loose to Very strict, an app setting of
    /// the ⚙ menu (<c>AppSettings.SimilarImages</c>). Set on real embeddings (§ Calibration).
    /// </summary>
    public static readonly float[] Cutoffs = [1.5f, 2.0f, 2.5f, 3.0f, 3.5f];

    /// <summary>The level of <see cref="Cutoffs"/> at first: Normal.</summary>
    public const int DefaultLevel = 2;

    /// <summary>What a text's result needs above the cutoff against the image's concepts: a nonsense query finds nothing at Normal.</summary>
    public const float TextMargin = 1.0f;

    // How far a result stands out at 0 % of closeness, and at 100 %.
    private const float NoCloseness = 1f;
    private const float FullCloseness = 5f;

    /// <summary>The cutoff of a strictness level, the default one for a level out of range.</summary>
    public static float CutoffOf(int level) => Cutoffs[level >= 0 && level < Cutoffs.Length ? level : DefaultLevel];

    /// <summary>The closeness shown for how far a result stands out: 0 at one spread, 1 at five, linear between.</summary>
    public static float Closeness(float standing) => Math.Clamp((standing - NoCloseness) / (FullCloseness - NoCloseness), 0, 1);

    /// <summary>The mean and the spread of an image's scores for the reference embeddings — the vocabulary's concepts.</summary>
    public static Spread SpreadOf(float[] embedding, IReadOnlyList<float[]> references)
    {
        double sum = 0, squares = 0;
        foreach (var reference in references)
        {
            double score = Embeddings.Dot(reference, embedding);
            sum += score;
            squares += score * score;
        }

        double mean = sum / references.Count;
        return new Spread((float)mean, (float)Math.Sqrt(Math.Max(0, squares / references.Count - mean * mean)));
    }

    /// <summary>The mean and the spread of scores.</summary>
    internal readonly record struct Spread(float Mean, float Deviation)
    {
        /// <summary>How far <paramref name="score"/> stands out of them, in spreads; 0 when they do not spread.</summary>
        public float Of(float score) => this.Deviation > 0 ? (score - this.Mean) / this.Deviation : 0;
    }
}

namespace ImageGridFusion.Explorer;

/// <summary>
/// How good a picture of its person a face makes, in (0, 1]: its score, its size and how frontal it is,
/// each mapped to (0, 1] and multiplied. A poor face — small, unsure, turned — gives a noisy embedding: it
/// weighs less in a group's sums and needs a closer similarity to join one (<see cref="FaceGrouping"/>).
/// See workfiles/20261028-face-grouping-accuracy.md § Quality.
/// </summary>
internal static class FaceQuality
{
    /// <summary>Under it, a face close to no one is a low-quality face, set apart from the faces alone.</summary>
    public const float LowFloor = 0.35f;

    // The score from which a face is sure, and what the lowest score kept gives.
    private const float SureScore = 0.92f;
    private const float LeastScoreFactor = 0.55f;

    // The side from which a face is large enough — SFace's input — and what the narrowest kept gives.
    private const float LargeSide = 112f;
    private const float LeastSideFactor = 0.45f;

    // The nose's offset from the eyes' midpoint, relative to the eyes' distance, from which a face is
    // turned as far as counts, and what that gives.
    private const float TurnedOffset = 0.5f;
    private const float LeastPoseFactor = 0.3f;

    public static float Of(FaceBox face) => ScoreFactor(face.Score) * SideFactor(face.Side) * PoseFactor(face.Landmarks);

    /// <summary>The score's share: the lowest kept (<see cref="FaceAnalyzer.MinScore"/>) to sure.</summary>
    public static float ScoreFactor(float score) => Ramp(score, FaceAnalyzer.MinScore, SureScore, LeastScoreFactor);

    /// <summary>The size's share: the narrowest kept (<see cref="FaceAnalyzer.MinFaceSide"/>) to SFace's input.</summary>
    public static float SideFactor(float side) => Ramp(side, FaceAnalyzer.MinFaceSide, LargeSide, LeastSideFactor);

    /// <summary>
    /// How frontal a face is: the nose's horizontal offset from the eyes' midpoint, measured along the eyes'
    /// line and relative to their distance — 0 facing, about 0.5 in three-quarter view. 1 without landmarks.
    /// </summary>
    public static float PoseFactor(PointF[] landmarks)
    {
        if (landmarks.Length < 3)
        {
            return 1;
        }

        var (right, left, nose) = (landmarks[0], landmarks[1], landmarks[2]);
        float ex = left.X - right.X;
        float ey = left.Y - right.Y;
        float distance = MathF.Sqrt(ex * ex + ey * ey);
        if (distance <= 0)
        {
            return LeastPoseFactor;
        }

        float along = ((nose.X - (right.X + left.X) / 2) * ex + (nose.Y - (right.Y + left.Y) / 2) * ey) / (distance * distance);
        return Ramp(-MathF.Abs(along), -TurnedOffset, 0, LeastPoseFactor);
    }

    /// <summary><paramref name="least"/> at <paramref name="from"/> or below, 1 at <paramref name="to"/> or above, linear between.</summary>
    private static float Ramp(float value, float from, float to, float least) =>
        least + (1 - least) * Math.Clamp((value - from) / (to - from), 0, 1);
}

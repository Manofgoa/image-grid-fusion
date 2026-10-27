using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ImageGridFusion.Explorer;

/// <summary>
/// A face found in an image: its box in fractions of the image as decoded — oriented as its EXIF says —
/// YuNet's score, and SFace's embedding, 128 values of length 1: two faces are the same person when
/// their dot product reaches <see cref="FaceGrouping.SameThreshold"/>.
/// </summary>
internal sealed record FaceBox(RectangleF Bounds, float Score, float[] Embedding);

/// <summary>
/// The face pass's engine, from the downloaded models (<see cref="FaceEngineFiles"/>): YuNet finds the
/// faces and their five landmarks on the image fitted into its 640 × 640 input, each face kept is
/// aligned from its landmarks onto SFace's 112 × 112 template — read from the full image — and SFace
/// gives its embedding. Created when the pass starts, disposed when it ends. See
/// workfiles/20261027-face-detection.md § Face Pass.
/// </summary>
internal sealed class FaceAnalyzer : IDisposable
{
    /// <summary>The lowest score of a face kept: YuNet scores a clear face above 0.9, a small one about 0.8, the noise of a crowd below 0.6.</summary>
    public const float MinScore = 0.75f;

    /// <summary>The narrowest face kept, in pixels of the image decoded: SFace tells smaller ones apart poorly.</summary>
    public const int MinFaceSide = 40;

    // YuNet's fixed input, its strides, and the overlap past which two boxes are one face.
    private const int DetectionSide = 640;
    private static readonly int[] Strides = [8, 16, 32];
    private const float NmsOverlap = 0.3f;

    // The most faces kept per image, the best first.
    private const int MaxFaces = 64;

    // SFace's input, and where its template puts the eyes, the nose and the mouth's corners, in that order.
    private const int AlignedSide = 112;
    private static readonly PointF[] Template =
    [
        new(38.2946f, 51.6963f), new(73.5318f, 51.5014f), new(56.0252f, 71.7366f), new(41.5493f, 92.3655f), new(70.7299f, 92.2041f),
    ];

    private readonly InferenceSession _detector;
    private readonly InferenceSession _recognizer;

    /// <summary>
    /// Loads the models — ONNX Runtime's native DLL resolved from the OCR engine's folder first, before
    /// anything touches it. Throws when they do not load.
    /// </summary>
    public FaceAnalyzer()
        : this(FaceEngineFiles.PathOf(FaceEngineFiles.Detection), FaceEngineFiles.PathOf(FaceEngineFiles.Recognition))
    {
    }

    /// <summary>Loads the models from <paramref name="detection"/> and <paramref name="recognition"/>.</summary>
    internal FaceAnalyzer(string detection, string recognition)
    {
        OcrEngineFiles.ResolveNativeLibraries();
        using var options = new SessionOptions { LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_ERROR, IntraOpNumThreads = 2 };
        this._detector = new InferenceSession(detection, options);
        try
        {
            this._recognizer = new InferenceSession(recognition, options);
        }
        catch
        {
            this._detector.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        this._detector.Dispose();
        this._recognizer.Dispose();
    }

    /// <summary>The faces in <paramref name="pixels"/> — BGRA premultiplied, row after row — the best first.</summary>
    public FaceBox[] Analyze(byte[] pixels, int width, int height)
    {
        if (width < MinFaceSide || height < MinFaceSide)
        {
            return [];
        }

        var faces = new List<FaceBox>();
        foreach (var found in this.Detect(pixels, width, height))
        {
            var bounds = RectangleF.Intersect(found.Box, new RectangleF(0, 0, width, height));
            if (bounds.Width < MinFaceSide || bounds.Height < MinFaceSide)
            {
                continue;
            }

            var embedding = this.Embed(Align(pixels, width, height, found.Landmarks));
            faces.Add(new FaceBox(new RectangleF(bounds.X / width, bounds.Y / height, bounds.Width / width, bounds.Height / height), found.Score, embedding));
        }

        return [.. faces];
    }

    /// <summary>The dot product of two embeddings of length 1: their cosine similarity.</summary>
    public static float Similarity(float[] a, float[] b)
    {
        var sum = System.Numerics.Vector<float>.Zero;
        int width = System.Numerics.Vector<float>.Count;
        int i = 0;
        for (; i <= a.Length - width; i += width)
        {
            sum += new System.Numerics.Vector<float>(a, i) * new System.Numerics.Vector<float>(b, i);
        }

        float dot = System.Numerics.Vector.Sum(sum);
        for (; i < a.Length; i++)
        {
            dot += a[i] * b[i];
        }

        return dot;
    }

    /// <summary>Scales <paramref name="vector"/> to length 1, in place; returns it.</summary>
    public static float[] Normalize(float[] vector)
    {
        double length = Math.Sqrt(vector.Sum(v => (double)v * v));
        if (length > 0)
        {
            for (int i = 0; i < vector.Length; i++)
            {
                vector[i] = (float)(vector[i] / length);
            }
        }

        return vector;
    }

    /// <summary>
    /// YuNet on the image fitted into its input, its top-left corner, the rest black: BGR, 0 to 255, as
    /// OpenCV feeds it. Each stride's grid gives a score — the root of its class and objectness scores —
    /// a box and five landmarks per cell; those above <see cref="MinScore"/> kept, overlapping ones merged
    /// to the best. In the image's pixels.
    /// </summary>
    private List<(RectangleF Box, PointF[] Landmarks, float Score)> Detect(byte[] pixels, int width, int height)
    {
        float scale = (float)DetectionSide / Math.Max(width, height);
        int fittedWidth = Math.Min(DetectionSide, Math.Max(1, (int)Math.Round(width * scale)));
        int fittedHeight = Math.Min(DetectionSide, Math.Max(1, (int)Math.Round(height * scale)));
        var input = new DenseTensor<float>([1, 3, DetectionSide, DetectionSide]);
        var data = input.Buffer.Span;
        int plane = DetectionSide * DetectionSide;
        Span<float> bgr = stackalloc float[3];
        for (int y = 0; y < fittedHeight; y++)
        {
            for (int x = 0; x < fittedWidth; x++)
            {
                SampleBgr(pixels, width, height, (x + 0.5f) / scale - 0.5f, (y + 0.5f) / scale - 0.5f, bgr);
                int at = y * DetectionSide + x;
                data[at] = bgr[0];
                data[plane + at] = bgr[1];
                data[2 * plane + at] = bgr[2];
            }
        }

        string inputName = this._detector.InputMetadata.Keys.First();
        using var results = this._detector.Run([NamedOnnxValue.CreateFromTensor(inputName, input)]);
        var outputs = results.ToDictionary(r => r.Name, r => r.AsTensor<float>());
        var candidates = new List<(RectangleF Box, PointF[] Landmarks, float Score)>();
        foreach (int stride in Strides)
        {
            var cls = outputs[$"cls_{stride}"];
            var obj = outputs[$"obj_{stride}"];
            var bbox = outputs[$"bbox_{stride}"];
            var kps = outputs[$"kps_{stride}"];
            int columns = DetectionSide / stride;
            int cells = columns * columns;
            for (int cell = 0; cell < cells; cell++)
            {
                float score = MathF.Sqrt(Math.Clamp(cls[0, cell, 0], 0, 1) * Math.Clamp(obj[0, cell, 0], 0, 1));
                if (score < MinScore)
                {
                    continue;
                }

                int column = cell % columns;
                int row = cell / columns;
                float cx = (column + bbox[0, cell, 0]) * stride;
                float cy = (row + bbox[0, cell, 1]) * stride;
                float w = MathF.Exp(bbox[0, cell, 2]) * stride;
                float h = MathF.Exp(bbox[0, cell, 3]) * stride;
                var landmarks = new PointF[5];
                for (int n = 0; n < 5; n++)
                {
                    landmarks[n] = new PointF((kps[0, cell, 2 * n] + column) * stride / scale, (kps[0, cell, 2 * n + 1] + row) * stride / scale);
                }

                candidates.Add((new RectangleF((cx - w / 2) / scale, (cy - h / 2) / scale, w / scale, h / scale), landmarks, score));
            }
        }

        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        var kept = new List<(RectangleF Box, PointF[] Landmarks, float Score)>();
        foreach (var candidate in candidates)
        {
            if (kept.Count < MaxFaces && kept.All(k => Overlap(k.Box, candidate.Box) <= NmsOverlap))
            {
                kept.Add(candidate);
            }
        }

        return kept;
    }

    /// <summary>
    /// The face turned, scaled and moved so its landmarks fall on SFace's template — the similarity
    /// transform closest to them in least squares — read from the full image into 112 × 112 pixels: RGB,
    /// 0 to 255, plane after plane, as SFace takes it.
    /// </summary>
    internal static float[] Align(byte[] pixels, int width, int height, PointF[] landmarks)
    {
        var (a, b, tx, ty) = SimilarityTransform(landmarks, Template);

        // The inverse of x' = a x − b y + tx, y' = b x + a y + ty, from the template back to the image.
        float norm = a * a + b * b;
        float ia = a / norm;
        float ib = -b / norm;
        var aligned = new float[3 * AlignedSide * AlignedSide];
        int plane = AlignedSide * AlignedSide;
        Span<float> bgr = stackalloc float[3];
        for (int v = 0; v < AlignedSide; v++)
        {
            for (int u = 0; u < AlignedSide; u++)
            {
                float dx = u - tx;
                float dy = v - ty;
                float x = ia * dx - ib * dy;
                float y = ib * dx + ia * dy;
                SampleBgr(pixels, width, height, x, y, bgr);
                int at = v * AlignedSide + u;
                aligned[at] = bgr[2];
                aligned[plane + at] = bgr[1];
                aligned[2 * plane + at] = bgr[0];
            }
        }

        return aligned;
    }

    /// <summary>
    /// The similarity — a turn, a scale and a move, no mirror — taking <paramref name="from"/> closest to
    /// <paramref name="to"/> in least squares: x' = a x − b y + tx, y' = b x + a y + ty.
    /// </summary>
    internal static (float A, float B, float Tx, float Ty) SimilarityTransform(PointF[] from, PointF[] to)
    {
        float fx = from.Average(p => p.X), fy = from.Average(p => p.Y);
        float tx = to.Average(p => p.X), ty = to.Average(p => p.Y);
        double dot = 0, cross = 0, norm = 0;
        for (int i = 0; i < from.Length; i++)
        {
            double x = from[i].X - fx, y = from[i].Y - fy;
            double u = to[i].X - tx, v = to[i].Y - ty;
            dot += x * u + y * v;
            cross += x * v - y * u;
            norm += x * x + y * y;
        }

        float a = norm > 0 ? (float)(dot / norm) : 1;
        float b = norm > 0 ? (float)(cross / norm) : 0;
        return (a, b, tx - (a * fx - b * fy), ty - (b * fx + a * fy));
    }

    private float[] Embed(float[] aligned)
    {
        var input = new DenseTensor<float>(aligned, [1, 3, AlignedSide, AlignedSide]);
        string inputName = this._recognizer.InputMetadata.Keys.First();
        using var results = this._recognizer.Run([NamedOnnxValue.CreateFromTensor(inputName, input)]);
        return Normalize(results.First().AsEnumerable<float>().ToArray());
    }

    /// <summary>The share of the smaller box two boxes have in common, over their union: their intersection over union.</summary>
    private static float Overlap(RectangleF a, RectangleF b)
    {
        var common = RectangleF.Intersect(a, b);
        float shared = common.Width * common.Height;
        float union = a.Width * a.Height + b.Width * b.Height - shared;
        return union > 0 ? shared / union : 0;
    }

    /// <summary>
    /// The colour at (<paramref name="x"/>, <paramref name="y"/>) — pixel centres at whole numbers —
    /// interpolated between its four neighbours, as blue, green, red from 0 to 255; black outside the image.
    /// </summary>
    private static void SampleBgr(byte[] pixels, int width, int height, float x, float y, Span<float> bgr)
    {
        if (x < -0.5f || y < -0.5f || x > width - 0.5f || y > height - 0.5f)
        {
            bgr.Clear();
            return;
        }

        x = Math.Clamp(x, 0, width - 1);
        y = Math.Clamp(y, 0, height - 1);
        int x0 = (int)x;
        int y0 = (int)y;
        int x1 = Math.Min(x0 + 1, width - 1);
        int y1 = Math.Min(y0 + 1, height - 1);
        float fx = x - x0;
        float fy = y - y0;
        int p00 = (y0 * width + x0) * 4;
        int p01 = (y0 * width + x1) * 4;
        int p10 = (y1 * width + x0) * 4;
        int p11 = (y1 * width + x1) * 4;
        for (int i = 0; i < 3; i++)
        {
            float top = pixels[p00 + i] + (pixels[p01 + i] - pixels[p00 + i]) * fx;
            float bottom = pixels[p10 + i] + (pixels[p11 + i] - pixels[p10 + i]) * fx;
            bgr[i] = top + (bottom - top) * fy;
        }
    }
}

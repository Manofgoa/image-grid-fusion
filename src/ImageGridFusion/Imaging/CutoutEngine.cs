using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ImageGridFusion.Composition;
using ImageGridFusion.Explorer;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ImageGridFusion.Imaging;

/// <summary>
/// The Cutout's models, from the downloaded engine (<see cref="CutoutEngineFiles"/>): IS-Net, the
/// image's main subject, and MobileSAM, the object under the points clicked — its image encoder read once
/// per image, its mask decoder once per click. Each model is loaded the first time it is needed; used from
/// one thread at a time. See workfiles/20261029-cutout.md § The Mask.
/// </summary>
internal sealed class CutoutEngine : IDisposable
{
    // The side IS-Net reads the image at, squashed, and the longer side MobileSAM reads it at.
    private const int SubjectSide = 1024;
    private const int PickSide = 1024;

    // MobileSAM's prompt: its mask input, unused, and its embedding's shape.
    private const int MaskInputSide = 256;
    private static readonly int[] EmbeddingShape = [1, 256, 64, 64];

    // IS-Net's normalization, as rembg feeds it: each channel over the image's brightest value, minus a half.
    private const float Mean = 0.5f;

    private InferenceSession? _subject;
    private InferenceSession? _encoder;
    private InferenceSession? _single;
    private InferenceSession? _multi;

    public void Dispose()
    {
        this._subject?.Dispose();
        this._encoder?.Dispose();
        this._single?.Dispose();
        this._multi?.Dispose();
    }

    /// <summary>
    /// The main subject of <paramref name="image"/>: IS-Net on it squashed to 1024 × 1024 over white, its
    /// output stretched from its lowest value to its highest, as rembg reads it.
    /// </summary>
    public CutoutMask Subject(Bitmap image)
    {
        this._subject ??= Load(CutoutEngineFiles.SubjectModel);
        var input = new DenseTensor<float>([1, 3, SubjectSide, SubjectSide]);
        var span = input.Buffer.Span;
        int plane = SubjectSide * SubjectSide;
        var pixels = ReadPixels(image, SubjectSide, SubjectSide);
        float brightest = Math.Max(1, (float)pixels.Max());
        for (int at = 0; at < plane; at++)
        {
            span[at] = pixels[at * 3] / brightest - Mean;
            span[plane + at] = pixels[at * 3 + 1] / brightest - Mean;
            span[2 * plane + at] = pixels[at * 3 + 2] / brightest - Mean;
        }

        string name = this._subject.InputMetadata.Keys.First();
        string output = this._subject.OutputMetadata.Keys.First();
        using var results = this._subject.Run([NamedOnnxValue.CreateFromTensor(name, input)], [output]);
        var values = results.First().AsTensor<float>().ToArray();
        float low = values.Min(), high = values.Max();
        float range = Math.Max(high - low, 1e-6f);
        var alpha = new byte[plane];
        for (int i = 0; i < plane; i++)
        {
            alpha[i] = (byte)Math.Round(255 * (values[i] - low) / range);
        }

        return new CutoutMask(SubjectSide, SubjectSide, alpha);
    }

    /// <summary>MobileSAM's reading of <paramref name="image"/>, showing <paramref name="page"/>: its longer side at 1024, its colors as they are.</summary>
    public PickEmbedding Embed(Bitmap image, int page)
    {
        this._encoder ??= Load(CutoutEngineFiles.PickEncoder);
        double scale = (double)PickSide / Math.Max(image.Width, image.Height);
        var read = new Size(Math.Max(1, (int)Math.Round(image.Width * scale)), Math.Max(1, (int)Math.Round(image.Height * scale)));
        var input = new DenseTensor<float>([read.Height, read.Width, 3]);
        var span = input.Buffer.Span;
        var pixels = ReadPixels(image, read.Width, read.Height);
        for (int at = 0; at < pixels.Length; at++)
        {
            span[at] = pixels[at];
        }

        using var results = this._encoder.Run([NamedOnnxValue.CreateFromTensor("input_image", input)]);
        return new PickEmbedding(page, results.First().AsTensor<float>().ToArray(), read);
    }

    /// <summary>
    /// The object under <paramref name="points"/> — the included ones on it, the others off it — at the
    /// size the image was read at times <paramref name="scale"/>: a hover's outline asks for less. One
    /// point: the best of the multi-mask decoder's four; several: the single-mask decoder, as Segment
    /// Anything advises.
    /// </summary>
    public CutoutMask Pick(PickEmbedding embedding, IReadOnlyList<CutoutPoint> points, double scale = 1)
    {
        bool single = points.Count > 1;
        var decoder = single ? (this._single ??= Load(CutoutEngineFiles.PickSingle)) : (this._multi ??= Load(CutoutEngineFiles.PickMulti));
        var read = embedding.Read;
        var size = new Size(Math.Max(1, (int)Math.Round(read.Width * scale)), Math.Max(1, (int)Math.Round(read.Height * scale)));

        // A padding point closes the prompt when no box is given.
        var coords = new DenseTensor<float>([1, points.Count + 1, 2]);
        var labels = new DenseTensor<float>([1, points.Count + 1]);
        for (int i = 0; i < points.Count; i++)
        {
            coords[0, i, 0] = points[i].X * read.Width;
            coords[0, i, 1] = points[i].Y * read.Height;
            labels[0, i] = points[i].Included ? 1 : 0;
        }

        labels[0, points.Count] = -1;
        var inputs = new[]
        {
            NamedOnnxValue.CreateFromTensor("image_embeddings", new DenseTensor<float>(embedding.Values, EmbeddingShape)),
            NamedOnnxValue.CreateFromTensor("point_coords", coords),
            NamedOnnxValue.CreateFromTensor("point_labels", labels),
            NamedOnnxValue.CreateFromTensor("mask_input", new DenseTensor<float>([1, 1, MaskInputSide, MaskInputSide])),
            NamedOnnxValue.CreateFromTensor("has_mask_input", new DenseTensor<float>(new float[] { 0 }, [1])),
            NamedOnnxValue.CreateFromTensor("orig_im_size", new DenseTensor<float>(new float[] { size.Height, size.Width }, [2])),
        };
        using var results = decoder.Run(inputs);
        var masks = results.First(r => r.Name == "masks").AsTensor<float>();
        var scores = results.First(r => r.Name == "iou_predictions").AsTensor<float>();
        int best = 0;
        for (int m = 1; m < scores.Dimensions[1]; m++)
        {
            best = scores[0, m] > scores[0, best] ? m : best;
        }

        // The best mask's plane, read straight from the buffer: the tensor's indexer is slow per pixel.
        int plane = masks.Dimensions[2] * masks.Dimensions[3];
        var values = masks.ToArray();
        var alpha = new byte[plane];
        for (int i = 0; i < plane; i++)
        {
            alpha[i] = Opacity(values[best * plane + i]);
        }

        return new CutoutMask(masks.Dimensions[3], masks.Dimensions[2], alpha);
    }

    private static InferenceSession Load(string model)
    {
        OcrEngineFiles.ResolveNativeLibraries();

        // Without the arena, what a model takes while it runs is given back once it ends.
        using var options = new SessionOptions
        {
            LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_ERROR,
            IntraOpNumThreads = 2,
            EnableCpuMemArena = false,
            EnableMemoryPattern = false,
        };
        return new InferenceSession(CutoutEngineFiles.PathOf(model), options);
    }

    private static byte Opacity(float logit) => (byte)Math.Round(255 / (1 + Math.Exp(-logit)));

    /// <summary>
    /// <paramref name="image"/> drawn at <paramref name="width"/> × <paramref name="height"/> over white:
    /// red, green and blue per pixel, row after row.
    /// </summary>
    private static byte[] ReadPixels(Bitmap image, int width, int height)
    {
        using var small = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(small))
        using (var attributes = new ImageAttributes())
        {
            g.Clear(Color.White);
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            attributes.SetWrapMode(WrapMode.TileFlipXY);
            g.DrawImage(image, new Rectangle(0, 0, width, height), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
        }

        var pixels = new byte[width * height * 3];
        var data = small.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            var row = new byte[width * 3];
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, row.Length);
                for (int x = 0; x < width; x++)
                {
                    // GDI+ stores blue, green, red.
                    int at = (y * width + x) * 3;
                    pixels[at] = row[x * 3 + 2];
                    pixels[at + 1] = row[x * 3 + 1];
                    pixels[at + 2] = row[x * 3];
                }
            }
        }
        finally
        {
            small.UnlockBits(data);
        }

        return pixels;
    }
}

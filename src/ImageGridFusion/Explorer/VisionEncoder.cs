using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace ImageGridFusion.Explorer;

/// <summary>
/// SigLIP 2's vision model, from the downloaded engine (<see cref="VisionEngineFiles"/>): an image's
/// embedding — 768 values of length 1, in the space the text model's share — from the image squashed to
/// 256 × 256 over white, its channels scaled to [-1, 1]. Created by the Vision pass when it starts,
/// disposed when it ends; for one image, by a similar image search. See
/// workfiles/20261027-image-content-analysis.md § Passes.
/// </summary>
internal sealed class VisionEncoder : IDisposable
{
    /// <summary>The embeddings' length.</summary>
    public const int Dimensions = 768;

    // The model's fixed input side.
    private const int Side = 256;

    private readonly InferenceSession _session;

    /// <summary>Loads the model — ONNX Runtime's native DLL resolved from the OCR engine's folder first. Throws when it does not load.</summary>
    public VisionEncoder()
    {
        OcrEngineFiles.ResolveNativeLibraries();
        using var options = new SessionOptions { LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_ERROR, IntraOpNumThreads = 2 };
        this._session = new InferenceSession(VisionEngineFiles.PathOf(VisionEngineFiles.VisionModel), options);
    }

    public void Dispose() => this._session.Dispose();

    /// <summary>The embedding of <paramref name="pixels"/> — BGRA premultiplied, row after row.</summary>
    public float[] Embed(byte[] pixels, int width, int height)
    {
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            using var source = new Bitmap(width, height, width * 4, PixelFormat.Format32bppPArgb, handle.AddrOfPinnedObject());
            return this.Embed(source);
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>The embedding of <paramref name="image"/>, a transparent part drawn over white.</summary>
    public float[] Embed(Image image)
    {
        using var small = new Bitmap(Side, Side, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(small))
        using (var attributes = new ImageAttributes())
        {
            g.Clear(Color.White);
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            attributes.SetWrapMode(WrapMode.TileFlipXY);
            g.DrawImage(image, new Rectangle(0, 0, Side, Side), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
        }

        var input = new DenseTensor<float>([1, 3, Side, Side]);
        var span = input.Buffer.Span;
        var data = small.LockBits(new Rectangle(0, 0, Side, Side), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            var row = new byte[Side * 3];
            for (int y = 0; y < Side; y++)
            {
                Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, row.Length);
                for (int x = 0; x < Side; x++)
                {
                    int at = y * Side + x;
                    span[at] = row[x * 3 + 2] / 127.5f - 1;
                    span[Side * Side + at] = row[x * 3 + 1] / 127.5f - 1;
                    span[2 * Side * Side + at] = row[x * 3] / 127.5f - 1;
                }
            }
        }
        finally
        {
            small.UnlockBits(data);
        }

        using var results = this._session.Run([NamedOnnxValue.CreateFromTensor("pixel_values", input)]);
        return Embeddings.Normalized(results.First(r => r.Name == "pooler_output").AsTensor<float>().ToArray());
    }
}

/// <summary>
/// SigLIP 2's text model and its tokenizer (<see cref="VisionEngineFiles"/>): a text's embedding, in the
/// space the images' share — the text lowercased, tokenized with Gemma's SentencePiece, no BOS, EOS (1)
/// appended, padded with 0 to 64 tokens. Thread-safe. See workfiles/20261027-image-content-analysis.md § Models.
/// </summary>
internal sealed class TextEncoder : IDisposable
{
    private const int Length = 64;
    private const int Eos = 1;

    private readonly InferenceSession _session;
    private readonly SentencePieceTokenizer _tokenizer;

    /// <summary>Loads the model and the tokenizer. Throws when they do not load.</summary>
    public TextEncoder()
    {
        OcrEngineFiles.ResolveNativeLibraries();
        using (var model = File.OpenRead(VisionEngineFiles.PathOf(VisionEngineFiles.Tokenizer)))
        {
            this._tokenizer = SentencePieceTokenizer.Create(model, false, false);
        }

        using var options = new SessionOptions { LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_ERROR, IntraOpNumThreads = 2 };
        this._session = new InferenceSession(VisionEngineFiles.PathOf(VisionEngineFiles.TextModel), options);
    }

    public void Dispose() => this._session.Dispose();

    /// <summary>The embedding of <paramref name="text"/>, cut to the tokens the model takes.</summary>
    public float[] Embed(string text)
    {
        var ids = new long[Length];
        int count = 0;
        foreach (int id in this._tokenizer.EncodeToIds(text.ToLowerInvariant(), false, false))
        {
            if (count == Length - 1)
            {
                break;
            }

            ids[count++] = id;
        }

        ids[count] = Eos;
        using var results = this._session.Run([NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(ids, [1, Length]))]);
        return Embeddings.Normalized(results.First(r => r.Name == "pooler_output").AsTensor<float>().ToArray());
    }
}

/// <summary>Embeddings' arithmetic: vectors of length 1 compared by their dot product.</summary>
internal static class Embeddings
{
    /// <summary><paramref name="vector"/> scaled to length 1, in place.</summary>
    public static float[] Normalized(float[] vector)
    {
        float length = MathF.Sqrt(Dot(vector, vector));
        if (length > 0)
        {
            for (int i = 0; i < vector.Length; i++)
            {
                vector[i] /= length;
            }
        }

        return vector;
    }

    /// <summary>The dot product: the cosine similarity of two embeddings of length 1.</summary>
    public static float Dot(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        var sum = Vector<float>.Zero;
        int width = Vector<float>.Count;
        int i = 0;
        for (; i <= a.Length - width; i += width)
        {
            sum += new Vector<float>(a[i..]) * new Vector<float>(b[i..]);
        }

        float dot = Vector.Sum(sum);
        for (; i < a.Length; i++)
        {
            dot += a[i] * b[i];
        }

        return dot;
    }
}

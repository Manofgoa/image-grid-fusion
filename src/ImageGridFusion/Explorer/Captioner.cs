using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ImageGridFusion.Explorer;

/// <summary>
/// Florence-2 base, from the downloaded caption engine (<see cref="CaptionEngineFiles"/>): a sentence in
/// English saying what an image shows — its <c>&lt;CAPTION&gt;</c> task. The image squashed to 768 × 768,
/// normalized as ImageNet's, through the vision encoder; its features before the task's prompt, embedded,
/// through the encoder; then the decoder, greedily, one token at a time — the first forced to BOS, no
/// trigram twice — up to EOS or <see cref="MaxTokens"/>; the tokens turned back into text by the byte-level
/// BPE's vocabulary. Created by the Caption pass when it starts, disposed when it ends. See
/// workfiles/20261027-image-content-analysis.md § Passes.
/// </summary>
internal sealed class Captioner : IDisposable
{
    /// <summary>The most tokens a caption has.</summary>
    public const int MaxTokens = 48;

    private const int Side = 768;
    private const long Bos = 0;
    private const long Eos = 2;
    private const int NoRepeatNgram = 3;

    // The <CAPTION> task's prompt, "What does the image describe?", as the tokenizer gives it.
    private static readonly long[] Prompt = [0, 2264, 473, 5, 2274, 6190, 116, 2];

    // ImageNet's mean and standard deviation, Florence-2's processor's.
    private static readonly float[] Mean = [0.485f, 0.456f, 0.406f];
    private static readonly float[] Deviation = [0.229f, 0.224f, 0.225f];

    private readonly InferenceSession _vision;
    private readonly InferenceSession _embed;
    private readonly InferenceSession _encoder;
    private readonly InferenceSession _decoder;
    private readonly string?[] _tokens;

    /// <summary>Loads the models and the vocabulary — ONNX Runtime's native DLL resolved from the OCR engine's folder first. Throws when they do not load.</summary>
    public Captioner()
        : this(VisionEngineFiles.Folder)
    {
    }

    /// <summary>Loads the models and the vocabulary from <paramref name="folder"/>.</summary>
    internal Captioner(string folder)
    {
        OcrEngineFiles.ResolveNativeLibraries();
        this._tokens = ReadVocabulary(Path.Combine(folder, CaptionEngineFiles.Vocabulary));
        using var options = new SessionOptions { LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_ERROR, IntraOpNumThreads = 2 };
        var sessions = new List<InferenceSession>();
        try
        {
            foreach (string model in CaptionEngineFiles.Models)
            {
                sessions.Add(new InferenceSession(Path.Combine(folder, model), options));
            }
        }
        catch
        {
            sessions.ForEach(s => s.Dispose());
            throw;
        }

        (this._vision, this._embed, this._encoder, this._decoder) = (sessions[0], sessions[1], sessions[2], sessions[3]);
    }

    public void Dispose()
    {
        this._vision.Dispose();
        this._embed.Dispose();
        this._encoder.Dispose();
        this._decoder.Dispose();
    }

    /// <summary>The caption of <paramref name="pixels"/> — BGRA premultiplied, row after row.</summary>
    public string Describe(byte[] pixels, int width, int height)
    {
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            using var source = new Bitmap(width, height, width * 4, PixelFormat.Format32bppPArgb, handle.AddrOfPinnedObject());
            return this.Describe(source);
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>The caption of <paramref name="image"/>, a transparent part drawn over white.</summary>
    public string Describe(Image image)
    {
        var features = this.Features(image);
        int imageLength = features.Dimensions[1];
        var prompt = this.Embed(Prompt);
        int length = imageLength + Prompt.Length;

        // The image's features, then the prompt's embeddings: the encoder's input.
        var inputs = new DenseTensor<float>([1, length, 768]);
        features.Buffer.Span.CopyTo(inputs.Buffer.Span);
        prompt.Buffer.Span.CopyTo(inputs.Buffer.Span[(imageLength * 768)..]);
        var mask = new DenseTensor<long>(Enumerable.Repeat(1L, length).ToArray(), [1, length]);
        DenseTensor<float> encoded;
        using (var results = this._encoder.Run([NamedOnnxValue.CreateFromTensor("attention_mask", mask), NamedOnnxValue.CreateFromTensor("inputs_embeds", inputs)]))
        {
            encoded = results.First(r => r.Name == "last_hidden_state").AsTensor<float>().ToDenseTensor();
        }

        var generated = new List<long> { Eos };
        for (int step = 0; step < MaxTokens; step++)
        {
            var embedded = this.Embed(generated.ToArray());
            long next;
            using (var results = this._decoder.Run([
                NamedOnnxValue.CreateFromTensor("encoder_attention_mask", mask),
                NamedOnnxValue.CreateFromTensor("encoder_hidden_states", encoded),
                NamedOnnxValue.CreateFromTensor("inputs_embeds", embedded)]))
            {
                var logits = results.First(r => r.Name == "logits").AsTensor<float>();
                next = step == 0 ? Bos : this.Pick(logits, generated);
            }

            generated.Add(next);
            if (next == Eos)
            {
                break;
            }
        }

        return this.Text(generated);
    }

    /// <summary>The image's features from the vision encoder.</summary>
    private DenseTensor<float> Features(Image image)
    {
        using var small = new Bitmap(Side, Side, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(small))
        using (var attributes = new ImageAttributes())
        {
            g.Clear(Color.White);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
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
                    for (int c = 0; c < 3; c++)
                    {
                        // RGB from the BGR bytes.
                        span[c * Side * Side + at] = (row[x * 3 + 2 - c] / 255f - Mean[c]) / Deviation[c];
                    }
                }
            }
        }
        finally
        {
            small.UnlockBits(data);
        }

        using var results = this._vision.Run([NamedOnnxValue.CreateFromTensor("pixel_values", input)]);
        return results.First(r => r.Name == "image_features").AsTensor<float>().ToDenseTensor();
    }

    /// <summary>The tokens' embeddings.</summary>
    private DenseTensor<float> Embed(long[] ids)
    {
        using var results = this._embed.Run([NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(ids, [1, ids.Length]))]);
        return results.First(r => r.Name == "inputs_embeds").AsTensor<float>().ToDenseTensor();
    }

    /// <summary>The most likely next token after <paramref name="generated"/>, none that would repeat a trigram already there.</summary>
    private long Pick(Tensor<float> logits, List<long> generated)
    {
        int last = logits.Dimensions[1] - 1;
        int vocabulary = logits.Dimensions[2];
        var banned = new HashSet<long>();
        if (generated.Count >= NoRepeatNgram - 1)
        {
            int prefix = NoRepeatNgram - 1;
            for (int i = 0; i + prefix < generated.Count; i++)
            {
                bool same = true;
                for (int k = 0; k < prefix; k++)
                {
                    same &= generated[i + k] == generated[generated.Count - prefix + k];
                }

                if (same)
                {
                    banned.Add(generated[i + prefix]);
                }
            }
        }

        long best = Eos;
        float score = float.NegativeInfinity;
        for (int id = 0; id < vocabulary; id++)
        {
            float value = logits[0, last, id];
            if (value > score && !banned.Contains(id))
            {
                score = value;
                best = id;
            }
        }

        return best;
    }

    /// <summary>The caption's text: the tokens of the vocabulary, special ones left out, their bytes turned back into UTF-8.</summary>
    private string Text(List<long> ids)
    {
        var bytes = new List<byte>();
        foreach (long id in ids)
        {
            if (id > 3 && id < this._tokens.Length && this._tokens[id] is { } token && !(token.StartsWith('<') && token.EndsWith('>')))
            {
                foreach (char c in token)
                {
                    bytes.Add(ByteOf[c]);
                }
            }
        }

        return Encoding.UTF8.GetString(bytes.ToArray()).Trim();
    }

    private static string?[] ReadVocabulary(string path)
    {
        using var stream = File.OpenRead(path);
        var vocabulary = JsonSerializer.Deserialize<Dictionary<string, int>>(stream) ?? [];
        var tokens = new string?[vocabulary.Values.DefaultIfEmpty(0).Max() + 1];
        foreach (var (token, id) in vocabulary)
        {
            tokens[id] = token;
        }

        return tokens;
    }

    // The byte-level BPE's mapping of the 256 bytes onto printable characters, reversed (GPT-2's bytes_to_unicode).
    private static readonly Dictionary<char, byte> ByteOf = BuildByteOf();

    private static Dictionary<char, byte> BuildByteOf()
    {
        var map = new Dictionary<char, byte>();
        int extra = 0;
        for (int b = 0; b < 256; b++)
        {
            bool printable = b is >= '!' and <= '~' or >= 0xA1 and <= 0xAC or >= 0xAE and <= 0xFF;
            map[(char)(printable ? b : 256 + extra++)] = (byte)b;
        }

        return map;
    }
}

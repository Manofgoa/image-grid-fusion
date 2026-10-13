using System.Runtime.InteropServices;
using System.Text;
using RapidOcrNet;
using SkiaSharp;

namespace ImageGridFusion.Explorer;

/// <summary>
/// The RapidOCR pass's engine: PaddleOCR's PP-OCRv5 latin models through ONNX Runtime
/// (<c>RapidOcrNet</c>), loaded from the downloaded engine (<see cref="OcrEngineFiles"/>). Each text block
/// it reads is sure, unsure or dropped by its characters' average score. Created when the pass starts,
/// disposed when it ends. See workfiles/20261013-complementary-ocr-passes.md § RapidOCR Pass.
/// </summary>
internal sealed class RapidRecognizer : IDisposable
{
    /// <summary>The average score from which a block is sure; below it, down to <see cref="KeptScore"/>, unsure.</summary>
    public const float SureScore = 0.8f;

    /// <summary>The average score under which a block is dropped as noise.</summary>
    public const float KeptScore = 0.6f;

    /// <summary>The fewest letters or digits a block holds to be kept.</summary>
    public const int MinCharacters = 2;

    private static readonly RapidOcrOptions Options = RapidOcrOptions.Default with { ReturnWordBox = true, TextScore = KeptScore };

    private readonly RapidOcr _ocr;

    /// <summary>
    /// Loads the models — the engine's native DLLs resolved from its folder first, before anything
    /// touches them: another libSkiaSharp on the PATH (Git Credential Manager ships one) would be taken
    /// otherwise. Throws when they do not load.
    /// </summary>
    public RapidRecognizer()
    {
        OcrEngineFiles.ResolveNativeLibraries();
        this._ocr = new RapidOcr();
        try
        {
            this._ocr.InitModels(
                OcrEngineFiles.PathOf(OcrEngineFiles.Detection),
                OcrEngineFiles.PathOf(OcrEngineFiles.Classification),
                OcrEngineFiles.PathOf(OcrEngineFiles.Recognition),
                OcrEngineFiles.PathOf(OcrEngineFiles.Dictionary),
                0);
        }
        catch
        {
            this._ocr.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The sure and the unsure words read in <paramref name="pixels"/> — BGRA premultiplied, row after
    /// row — each block's words with their boxes in fractions of the image.
    /// </summary>
    public (ContentResult Sure, ContentResult Unsure) Recognize(byte[] pixels, int width, int height)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        Marshal.Copy(pixels, 0, bitmap.GetPixels(), Math.Min(pixels.Length, bitmap.ByteCount));
        var result = this._ocr.Detect(bitmap, Options);

        var sure = (Text: new StringBuilder(), Boxes: new List<WordBox>());
        var unsure = (Text: new StringBuilder(), Boxes: new List<WordBox>());
        foreach (var block in result.TextBlocks)
        {
            float score = block.CharScores is { Length: > 0 } scores ? scores.Average() : 0;
            if (score < KeptScore || block.Text.Count(char.IsLetterOrDigit) < MinCharacters)
            {
                continue;
            }

            var target = score >= SureScore ? sure : unsure;
            target.Text.Append(block.Text).Append(' ');
            if (block.WordResults is { Length: > 0 } words)
            {
                foreach (var word in words)
                {
                    target.Boxes.Add(new WordBox(word.Text, Bounds(word.BoxPoints, width, height)));
                }
            }
            else
            {
                target.Boxes.Add(new WordBox(block.Text, Bounds(block.BoxPoints, width, height)));
            }
        }

        return (new ContentResult(sure.Text.ToString(), sure.Boxes), new ContentResult(unsure.Text.ToString(), unsure.Boxes));
    }

    public void Dispose() => this._ocr.Dispose();

    /// <summary>The rectangle around a polygon, in fractions of the image.</summary>
    private static RectangleF Bounds(SKPointI[] points, int width, int height)
    {
        if (points.Length == 0)
        {
            return RectangleF.Empty;
        }

        float w = Math.Max(1, width);
        float h = Math.Max(1, height);
        int left = points.Min(p => p.X);
        int top = points.Min(p => p.Y);
        int right = points.Max(p => p.X);
        int bottom = points.Max(p => p.Y);
        return RectangleF.FromLTRB(left / w, top / h, right / w, bottom / h);
    }
}

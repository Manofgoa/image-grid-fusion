using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using ImageGridFusion.Imaging;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace ImageGridFusion.Explorer;

/// <summary>How a file's content text is had: recognised in its pixels, read as text, or not at all.</summary>
internal enum ContentKind
{
    None,
    Image,
    Pdf,
    Text,
}

/// <summary>
/// A file's content text as extracted, and where the OCR recognised its words — null for a file read
/// as text, empty for one recognised without a word.
/// </summary>
internal sealed record ContentResult(string Text, IReadOnlyList<WordBox>? Boxes)
{
    public static readonly ContentResult None = new("", null);

    /// <summary>Recognised without a word.</summary>
    public static ContentResult Empty => new("", []);
}

/// <summary>
/// Extracts a file's content text for the file explorer's search, on the calling thread, one pass at a
/// time (<see cref="ContentPass"/>): Windows' own text recognition (<see cref="OcrEngine"/>) on a still
/// image — a GIF's first frame — or a PDF's first page, in French and in English when Windows has those
/// languages, the text of a text or HTML file read; RapidOCR on the same pixels; Windows OCR again on
/// the colours inverted, then on the ring of the image unrolled into strips. Never throws for a file it
/// cannot read: it has no text. See workfiles/20260926-ocr-search.md § Extraction and
/// workfiles/20261013-complementary-ocr-passes.md § Indexing Passes.
/// </summary>
internal sealed class ContentExtractor : IDisposable
{
    /// <summary>The long side a PDF page is rendered at for the recognition: small print stays legible.</summary>
    private const int PdfLongSide = 2400;

    // The Inverted pass's copy: upscaled up to InvertedScale, its long side up to InvertedLongSide px.
    private const double InvertedScale = 2;
    private const int InvertedLongSide = 2400;

    // The Ring pass's band, in fractions of the inscribed circle's radius; the strips' height — the
    // band upscaled up to RingScale, at most RingHeight px — and the smallest one worth reading.
    private const double RingInner = 0.68;
    private const double RingScale = 2;
    private const int RingHeight = 200;
    private const int RingMinHeight = 24;

    // How many points of each edge of a strip's box are carried back along the arc.
    private const int ArcSamples = 6;

    /// <summary>The languages recognised, by their primary subtag, each by an engine of its own.</summary>
    private static readonly string[] Languages = ["fr", "en"];

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".jpe", ".jfif", ".bmp", ".dib", ".gif", ".tif", ".tiff", ".webp", ".heic", ".heif", ".avif", ".ico", ".jxr", ".wdp",
    };

    // Files that are never text: not even opened.
    private static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".m4v", ".mov", ".avi", ".mkv", ".webm", ".wmv", ".mpg", ".mpeg", ".3gp", ".mp3", ".m4a", ".aac", ".wav", ".flac", ".ogg", ".opus", ".wma",
        ".zip", ".7z", ".rar", ".gz", ".tar", ".exe", ".dll", ".msi", ".iso", ".psd", ".docx", ".xlsx", ".pptx",
    };

    private readonly Lazy<OcrEngine[]> _engines = new(CreateEngines);
    private RapidRecognizer? _rapid;

    /// <summary>How the file's content text is had, from its extension.</summary>
    public static ContentKind KindOf(string path)
    {
        string extension = Path.GetExtension(path);
        return ImageExtensions.Contains(extension) ? ContentKind.Image
            : extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase) ? ContentKind.Pdf
            : MediaExtensions.Contains(extension) ? ContentKind.None
            : ContentKind.Text;
    }

    /// <summary>Whether Windows has a text recognition language at all: without one, images and PDFs get no text.</summary>
    public bool CanRecognize => this._engines.Value.Length > 0;

    /// <summary>
    /// Starts RapidOCR for the RapidOCR pass, from the downloaded engine (<see cref="OcrEngineFiles"/>);
    /// null when it started, else why it cannot: not downloaded, or failing to load.
    /// </summary>
    public string? StartRapid()
    {
        if (this._rapid is not null)
        {
            return null;
        }

        if (!OcrEngineFiles.IsAvailable)
        {
            return "not downloaded";
        }

        try
        {
            this._rapid = new RapidRecognizer();
            return null;
        }
        catch (Exception ex)
        {
            // A DLL that does not load, a model that does not parse: the pass waits for a working engine.
            return ex.Message;
        }
    }

    /// <summary>Stops RapidOCR once its pass is done, its memory given back.</summary>
    public void StopRapid()
    {
        this._rapid?.Dispose();
        this._rapid = null;
    }

    public void Dispose() => this.StopRapid();

    /// <summary>
    /// What <paramref name="pass"/> reads in the file — one result per slot of the pass
    /// (<see cref="ContentPasses.SlotsOf"/>): its content text, as raw as it came, and its words' boxes when
    /// an OCR read it; no text when it has none, or it cannot be read. The passes after the Windows one
    /// read images and PDFs only; RapidOCR's needs <see cref="StartRapid"/> first.
    /// </summary>
    public IReadOnlyList<ContentResult> Extract(string path, ContentPass pass)
    {
        var kind = KindOf(path);
        if (pass == ContentPass.Windows)
        {
            return [this.ExtractWindows(path, kind)];
        }

        int slots = ContentPasses.SlotsOf(pass).Length;
        if (kind is not (ContentKind.Image or ContentKind.Pdf))
        {
            return Empty(slots);
        }

        try
        {
            using var bitmap = Decode(path, kind);
            if (bitmap is null)
            {
                return Empty(slots);
            }

            return pass switch
            {
                ContentPass.Rapid => this.RecognizeRapid(bitmap),
                ContentPass.Inverted => [this.RecognizeInverted(bitmap)],
                _ => [this.RecognizeRing(bitmap)],
            };
        }
        catch (Exception)
        {
            // As the Windows pass: recognised without a word, not tried again until the file changes.
            return Empty(slots);
        }
    }

    /// <summary>
    /// The pixels the OCR reads in an image or a PDF's first page — BGRA premultiplied, row after row,
    /// oriented as its EXIF says — for the face pass and the face tiles; null for another file, or one
    /// that cannot be decoded. See workfiles/20261027-face-detection.md § Face Pass.
    /// </summary>
    public static (byte[] Pixels, int Width, int Height)? DecodePixels(string path)
    {
        var kind = KindOf(path);
        if (kind is not (ContentKind.Image or ContentKind.Pdf))
        {
            return null;
        }

        try
        {
            using var bitmap = Decode(path, kind);
            return bitmap is null ? null : (Pixels(bitmap), bitmap.PixelWidth, bitmap.PixelHeight);
        }
        catch (Exception)
        {
            // As the passes: WinRT reports unreadable or damaged files with assorted exception types.
            return null;
        }
    }

    private static ContentResult[] Empty(int slots) => Enumerable.Range(0, slots).Select(_ => ContentResult.Empty).ToArray();

    private ContentResult ExtractWindows(string path, ContentKind kind)
    {
        try
        {
            if (kind is ContentKind.Image or ContentKind.Pdf)
            {
                if (!this.CanRecognize)
                {
                    return ContentResult.Empty;
                }

                using var bitmap = Decode(path, kind);
                return bitmap is null ? ContentResult.Empty : this.RecognizeWindows(bitmap, r => Fraction(r, bitmap.PixelWidth, bitmap.PixelHeight));
            }

            return kind == ContentKind.Text ? new ContentResult(TextPages.TryReadContent(path) ?? "", null) : ContentResult.None;
        }
        catch (Exception)
        {
            // WinRT reports unreadable, unknown or damaged files with assorted exception types. Recognised
            // without a word, so it is not tried again until it changes.
            return kind is ContentKind.Image or ContentKind.Pdf ? ContentResult.Empty : ContentResult.None;
        }
    }

    /// <summary>The image or the PDF's first page decoded for the recognition; null for a PDF Windows cannot open.</summary>
    private static SoftwareBitmap? Decode(string path, ContentKind kind)
    {
        if (kind == ContentKind.Pdf)
        {
            using var page = PdfPages.TryRenderFirstPage(path, (int)Math.Min(PdfLongSide, OcrEngine.MaxImageDimension));
            return page is null ? null : Decode(page);
        }

        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var stream = file.AsRandomAccessStream();
        return Decode(stream);
    }

    /// <summary>
    /// The image decoded — oriented as its EXIF says, or downscaled to <see cref="OcrEngine.MaxImageDimension"/>
    /// when longer.
    /// </summary>
    private static SoftwareBitmap Decode(IRandomAccessStream stream)
    {
        var decoder = BitmapDecoder.CreateAsync(stream).AsTask().GetAwaiter().GetResult();
        uint width = decoder.PixelWidth;
        uint height = decoder.PixelHeight;
        uint longest = Math.Max(width, height);
        var transform = new BitmapTransform();
        var orientation = ExifOrientationMode.RespectExifOrientation;
        if (longest > OcrEngine.MaxImageDimension)
        {
            // The scaled size is given in the decoded frame's own axes: the EXIF turn is left out
            // rather than risk stretching the image across them.
            orientation = ExifOrientationMode.IgnoreExifOrientation;
            double scale = (double)OcrEngine.MaxImageDimension / longest;
            transform.ScaledWidth = (uint)Math.Max(1, Math.Floor(width * scale));
            transform.ScaledHeight = (uint)Math.Max(1, Math.Floor(height * scale));
            transform.InterpolationMode = BitmapInterpolationMode.Fant;
        }

        return decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            transform,
            orientation,
            ColorManagementMode.DoNotColorManage).AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// The words every engine recognises in <paramref name="bitmap"/>, their texts joined; each word's
    /// box, in the bitmap's pixels, carried back to the image by <paramref name="toImage"/> — in
    /// fractions of it — or dropped when it gives none.
    /// </summary>
    private ContentResult RecognizeWindows(SoftwareBitmap bitmap, Func<RectangleF, RectangleF?> toImage)
    {
        var text = new StringBuilder();
        var boxes = new List<WordBox>();
        foreach (var engine in this._engines.Value)
        {
            var result = engine.RecognizeAsync(bitmap).AsTask().GetAwaiter().GetResult();
            if (result.Text.Length > 0)
            {
                text.Append(result.Text).Append(' ');
            }

            foreach (var line in result.Lines)
            {
                foreach (var word in line.Words)
                {
                    var r = word.BoundingRect;
                    if (toImage(new RectangleF((float)r.X, (float)r.Y, (float)r.Width, (float)r.Height)) is { } bounds)
                    {
                        boxes.Add(new WordBox(word.Text, bounds));
                    }
                }
            }
        }

        return new ContentResult(text.ToString(), boxes);
    }

    private ContentResult[] RecognizeRapid(SoftwareBitmap bitmap)
    {
        if (this._rapid is not { } rapid)
        {
            return Empty(2);
        }

        var (sure, unsure) = rapid.Recognize(Pixels(bitmap), bitmap.PixelWidth, bitmap.PixelHeight);
        return [sure, unsure];
    }

    /// <summary>
    /// The Inverted pass: the colours inverted — light text on a dark background turned dark on light —
    /// upscaled, then read by Windows OCR; the boxes in fractions of the copy are the image's.
    /// </summary>
    private ContentResult RecognizeInverted(SoftwareBitmap bitmap)
    {
        if (!this.CanRecognize)
        {
            return ContentResult.Empty;
        }

        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;
        double scale = Math.Clamp((double)InvertedLongSide / Math.Max(width, height), 1, InvertedScale);
        int copyWidth = Math.Max(1, (int)Math.Round(width * scale));
        int copyHeight = Math.Max(1, (int)Math.Round(height * scale));
        byte[] source = Pixels(bitmap);
        byte[] copy = new byte[copyWidth * copyHeight * 4];
        for (int y = 0; y < copyHeight; y++)
        {
            for (int x = 0; x < copyWidth; x++)
            {
                int at = (y * copyWidth + x) * 4;
                Sample(source, width, height, (x + 0.5) / scale - 0.5, (y + 0.5) / scale - 0.5, copy, at);

                // Premultiplied: a colour inverted is its alpha minus itself.
                byte alpha = copy[at + 3];
                copy[at] = (byte)(alpha - copy[at]);
                copy[at + 1] = (byte)(alpha - copy[at + 1]);
                copy[at + 2] = (byte)(alpha - copy[at + 2]);
            }
        }

        using var inverted = FromPixels(copy, copyWidth, copyHeight);
        return this.RecognizeWindows(inverted, r => Fraction(r, copyWidth, copyHeight));
    }

    /// <summary>
    /// The Ring pass: the band along the edge of the image's inscribed circle — where a round badge
    /// writes — unrolled into two strips read by Windows OCR: clockwise with the outer edge on top, for a
    /// text along the top of the circle, and counter-clockwise with the inner edge on top, for one along
    /// its bottom. Each strip goes twice around, so a text across its seam shows whole once; each word's
    /// box is carried back along the arc it covers.
    /// </summary>
    private ContentResult RecognizeRing(SoftwareBitmap bitmap)
    {
        if (!this.CanRecognize)
        {
            return ContentResult.Empty;
        }

        var ring = Ring.Of(bitmap.PixelWidth, bitmap.PixelHeight);
        if (ring is null)
        {
            return ContentResult.Empty;
        }

        byte[] source = Pixels(bitmap);
        var text = new StringBuilder();
        var boxes = new List<WordBox>();
        foreach (bool clockwise in new[] { true, false })
        {
            var strip = ring.Value with { Clockwise = clockwise };
            byte[] pixels = new byte[strip.Width * strip.Height * 4];
            for (int y = 0; y < strip.Height; y++)
            {
                for (int x = 0; x < strip.Width; x++)
                {
                    var (sx, sy) = strip.ToImage(x + 0.5, y + 0.5);
                    Sample(source, bitmap.PixelWidth, bitmap.PixelHeight, sx - 0.5, sy - 0.5, pixels, (y * strip.Width + x) * 4);
                }
            }

            using var unrolled = FromPixels(pixels, strip.Width, strip.Height);
            var result = this.RecognizeWindows(unrolled, r => strip.Carried(r, bitmap.PixelWidth, bitmap.PixelHeight));
            text.Append(result.Text).Append(' ');
            boxes.AddRange(result.Boxes ?? []);
        }

        return new ContentResult(text.ToString(), boxes);
    }

    private static RectangleF Fraction(RectangleF r, int width, int height)
    {
        float w = Math.Max(1, width);
        float h = Math.Max(1, height);
        return new RectangleF(r.X / w, r.Y / h, r.Width / w, r.Height / h);
    }

    /// <summary>The bitmap's pixels, BGRA premultiplied, row after row.</summary>
    private static byte[] Pixels(SoftwareBitmap bitmap)
    {
        byte[] pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyToBuffer(pixels.AsBuffer());
        return pixels;
    }

    private static SoftwareBitmap FromPixels(byte[] pixels, int width, int height) =>
        SoftwareBitmap.CreateCopyFromBuffer(pixels.AsBuffer(), BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Premultiplied);

    /// <summary>The pixel at (<paramref name="x"/>, <paramref name="y"/>) — pixel centres at whole numbers — interpolated between its four neighbours, the edges extended.</summary>
    private static void Sample(byte[] source, int width, int height, double x, double y, byte[] target, int at)
    {
        x = Math.Clamp(x, 0, width - 1);
        y = Math.Clamp(y, 0, height - 1);
        int x0 = (int)x;
        int y0 = (int)y;
        int x1 = Math.Min(x0 + 1, width - 1);
        int y1 = Math.Min(y0 + 1, height - 1);
        double fx = x - x0;
        double fy = y - y0;
        int a = (y0 * width + x0) * 4;
        int b = (y0 * width + x1) * 4;
        int c = (y1 * width + x0) * 4;
        int d = (y1 * width + x1) * 4;
        for (int i = 0; i < 4; i++)
        {
            double top = source[a + i] + (source[b + i] - source[a + i]) * fx;
            double bottom = source[c + i] + (source[d + i] - source[c + i]) * fx;
            target[at + i] = (byte)Math.Round(top + (bottom - top) * fy);
        }
    }

    /// <summary>
    /// One engine per language of <see cref="Languages"/> Windows can recognise; else the user profile's
    /// languages; else none.
    /// </summary>
    private static OcrEngine[] CreateEngines()
    {
        var engines = new List<OcrEngine>();
        var available = OcrEngine.AvailableRecognizerLanguages;
        foreach (string wanted in Languages)
        {
            var language = available.FirstOrDefault(l => string.Equals(l.LanguageTag.Split('-')[0], wanted, StringComparison.OrdinalIgnoreCase));
            if (language is not null && OcrEngine.TryCreateFromLanguage(new Language(language.LanguageTag)) is { } engine)
            {
                engines.Add(engine);
            }
        }

        if (engines.Count == 0 && OcrEngine.TryCreateFromUserProfileLanguages() is { } profile)
        {
            engines.Add(profile);
        }

        return engines.ToArray();
    }

    /// <summary>
    /// A strip unrolling the band of the image's inscribed circle between <see cref="RingInner"/> and
    /// its edge: <see cref="Turn"/> pixels a turn, twice around, <see cref="Height"/> pixels across the
    /// band; clockwise from the bottom with the outer edge on top, or counter-clockwise from the top
    /// with the inner edge on top — either way the text along that side of the circle reads upright in
    /// the middle of a turn.
    /// </summary>
    private readonly record struct Ring(double CenterX, double CenterY, double Inner, double Outer, int Turn, int Height, bool Clockwise)
    {
        public int Width => this.Turn * 2;

        /// <summary>The strip for an image of this size; null when its band is too thin to read.</summary>
        public static Ring? Of(int width, int height)
        {
            double outer = Math.Min(width, height) / 2.0;
            double inner = outer * RingInner;
            double band = outer - inner;
            double scale = Math.Min(RingScale, RingHeight / band);
            double turn = 2 * Math.PI * (inner + outer) / 2 * scale;

            // Two turns within the OCR's longest side.
            if (turn * 2 > OcrEngine.MaxImageDimension)
            {
                scale *= OcrEngine.MaxImageDimension / (turn * 2);
                turn = OcrEngine.MaxImageDimension / 2.0;
            }

            int stripHeight = (int)Math.Round(band * scale);
            return stripHeight < RingMinHeight ? null : new Ring(width / 2.0, height / 2.0, inner, outer, (int)turn, stripHeight, true);
        }

        /// <summary>Where a point of the strip comes from in the image, in pixels.</summary>
        public (double X, double Y) ToImage(double x, double y)
        {
            double angle = this.Clockwise ? Math.PI / 2 + 2 * Math.PI * x / this.Turn : -Math.PI / 2 - 2 * Math.PI * x / this.Turn;
            double across = Math.Clamp(y / this.Height, 0, 1);
            double radius = this.Clockwise ? this.Outer - (this.Outer - this.Inner) * across : this.Inner + (this.Outer - this.Inner) * across;
            return (this.CenterX + radius * Math.Cos(angle), this.CenterY + radius * Math.Sin(angle));
        }

        /// <summary>A box of the strip carried back to the image: the rectangle around the arc it covers, in fractions of the image.</summary>
        public RectangleF? Carried(RectangleF box, int width, int height)
        {
            double left = double.MaxValue, top = double.MaxValue, right = double.MinValue, bottom = double.MinValue;
            for (int i = 0; i <= ArcSamples; i++)
            {
                double x = box.Left + box.Width * i / ArcSamples;
                foreach (double y in new[] { (double)box.Top, box.Bottom })
                {
                    var (ix, iy) = this.ToImage(x, y);
                    left = Math.Min(left, ix);
                    top = Math.Min(top, iy);
                    right = Math.Max(right, ix);
                    bottom = Math.Max(bottom, iy);
                }
            }

            return Fraction(RectangleF.FromLTRB((float)left, (float)top, (float)right, (float)bottom), width, height);
        }
    }
}

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
/// Extracts a file's content text for the file explorer's search, on the calling thread: Windows' own
/// text recognition (<see cref="OcrEngine"/>) on a still image — a GIF's first frame — or a PDF's first
/// page, in French and in English when Windows has those languages; the text of a text or HTML file,
/// read. Never throws for a file it cannot read: it has no text. See workfiles/20260926-ocr-search.md
/// § Extraction.
/// </summary>
internal sealed class ContentExtractor
{
    /// <summary>The long side a PDF page is rendered at for the recognition: small print stays legible.</summary>
    private const int PdfLongSide = 2400;

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

    /// <summary>The file's content text, as raw as it came; "" when it has none, or it cannot be read.</summary>
    public string Extract(string path)
    {
        try
        {
            return KindOf(path) switch
            {
                ContentKind.Image => this.RecognizeFile(path),
                ContentKind.Pdf => this.RecognizePdf(path),
                ContentKind.Text => TextPages.TryReadContent(path) ?? "",
                _ => "",
            };
        }
        catch (Exception)
        {
            // WinRT reports unreadable, unknown or damaged files with assorted exception types.
            return "";
        }
    }

    private string RecognizeFile(string path)
    {
        if (!this.CanRecognize)
        {
            return "";
        }

        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var stream = file.AsRandomAccessStream();
        return this.Recognize(stream);
    }

    private string RecognizePdf(string path)
    {
        if (!this.CanRecognize)
        {
            return "";
        }

        using var page = PdfPages.TryRenderFirstPage(path, (int)Math.Min(PdfLongSide, OcrEngine.MaxImageDimension));
        return page is null ? "" : this.Recognize(page);
    }

    /// <summary>
    /// The image decoded — oriented as its EXIF says, or downscaled to <see cref="OcrEngine.MaxImageDimension"/>
    /// when longer — then recognised by every engine, their texts joined.
    /// </summary>
    private string Recognize(IRandomAccessStream stream)
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

        using var bitmap = decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            transform,
            orientation,
            ColorManagementMode.DoNotColorManage).AsTask().GetAwaiter().GetResult();
        var text = new StringBuilder();
        foreach (var engine in this._engines.Value)
        {
            var result = engine.RecognizeAsync(bitmap).AsTask().GetAwaiter().GetResult();
            if (result.Text.Length > 0)
            {
                text.Append(result.Text).Append(' ');
            }
        }

        return text.ToString();
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
}

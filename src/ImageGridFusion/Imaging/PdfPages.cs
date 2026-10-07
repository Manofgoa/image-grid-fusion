using ImageGridFusion.Composition;
using Windows.Data.Pdf;
using Windows.Storage.Streams;

namespace ImageGridFusion.Imaging;

/// <summary>
/// Pages of a PDF, rendered whole by Windows (<see cref="PdfDocument"/>). The file is read into
/// memory, so it does not stay locked.
/// </summary>
public sealed class PdfPages : PageSource
{
    /// <summary>Long side of a rendered page: sharp in the export without widening the canvas on its own.</summary>
    private const int LongSide = 1600;

    private readonly InMemoryRandomAccessStream _stream;
    private readonly PdfDocument _document;

    private PdfPages(InMemoryRandomAccessStream stream, PdfDocument document)
    {
        _stream = stream;
        _document = document;
    }

    public override int Count => (int)_document.PageCount;

    public override string Label(int page) => $"{page + 1} / {Count}";

    /// <summary>One page per <see cref="Animation.StepDuration"/>, when there are several.</summary>
    public override TimeSpan LoopDuration => Count > 1 ? Animation.StepDuration * Count : TimeSpan.Zero;

    public override AnimationReader OpenAnimation() => new StepReader(Steps, Render);

    public override int PageAt(TimeSpan time) => StepReader.StepAt(Steps(), time);

    public override TimeSpan TimeOf(int page) => StepReader.StartOf(Steps(), page);

    private TimeSpan[] Steps() => Enumerable.Repeat(Animation.StepDuration, Count).ToArray();

    /// <summary>Returns null when the file is not a PDF Windows can open (encrypted, damaged…).</summary>
    public static PdfPages? TryOpen(string path)
    {
        if (!HasPdfHeader(path))
        {
            return null;
        }

        var stream = new InMemoryRandomAccessStream();
        try
        {
            using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(File.ReadAllBytes(path));
                writer.StoreAsync().AsTask().GetAwaiter().GetResult();
                writer.DetachStream();
            }

            var document = PdfDocument.LoadFromStreamAsync(stream).AsTask().GetAwaiter().GetResult();
            if (document.PageCount == 0)
            {
                stream.Dispose();
                return null;
            }

            return new PdfPages(stream, document);
        }
        catch (Exception)
        {
            // WinRT reports unreadable or protected documents with assorted exception types.
            stream.Dispose();
            return null;
        }
    }

    /// <summary>Serialized: the slider and the animation readers may render at the same time.</summary>
    public override Bitmap Render(int page)
    {
        lock (_document)
        {
            return RenderPage(page);
        }
    }

    private Bitmap RenderPage(int page)
    {
        using var output = this.RenderToStream(page, LongSide);
        using var image = Image.FromStream(output.AsStream());
        return ImageLoader.Copy(image);
    }

    /// <summary>
    /// The first page of a PDF, rendered with its long side at <paramref name="longSide"/> px, as an
    /// encoded image at the start of its stream — for the file explorer's text recognition; null when
    /// Windows cannot open the file.
    /// </summary>
    public static InMemoryRandomAccessStream? TryRenderFirstPage(string path, int longSide)
    {
        using var pages = TryOpen(path);
        if (pages is null)
        {
            return null;
        }

        lock (pages._document)
        {
            return pages.RenderToStream(0, longSide);
        }
    }

    /// <summary>A page rendered with its long side at <paramref name="longSide"/> px, the stream at its start.</summary>
    private InMemoryRandomAccessStream RenderToStream(int page, int longSide)
    {
        using var pdfPage = this._document.GetPage((uint)page);
        var size = pdfPage.Size;
        double scale = longSide / Math.Max(size.Width, size.Height);
        var options = new PdfPageRenderOptions
        {
            DestinationWidth = (uint)Math.Max(1, Math.Round(size.Width * scale)),
            DestinationHeight = (uint)Math.Max(1, Math.Round(size.Height * scale)),
        };

        var output = new InMemoryRandomAccessStream();
        try
        {
            pdfPage.RenderToStreamAsync(output, options).AsTask().GetAwaiter().GetResult();
            output.Seek(0);
            return output;
        }
        catch
        {
            output.Dispose();
            throw;
        }
    }

    public override void Dispose() => _stream.Dispose();

    private static bool HasPdfHeader(string path)
    {
        try
        {
            // The header may follow a little junk; readers accept it within the first kilobyte.
            using var file = File.OpenRead(path);
            var head = new byte[1024];
            int read = file.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
            return head.AsSpan(0, read).IndexOf("%PDF-"u8) >= 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

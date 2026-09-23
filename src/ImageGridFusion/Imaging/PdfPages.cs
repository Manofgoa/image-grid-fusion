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

    public override Bitmap Render(int page)
    {
        using var pdfPage = _document.GetPage((uint)page);
        var size = pdfPage.Size;
        double scale = LongSide / Math.Max(size.Width, size.Height);
        var options = new PdfPageRenderOptions
        {
            DestinationWidth = (uint)Math.Max(1, Math.Round(size.Width * scale)),
            DestinationHeight = (uint)Math.Max(1, Math.Round(size.Height * scale)),
        };

        using var output = new InMemoryRandomAccessStream();
        pdfPage.RenderToStreamAsync(output, options).AsTask().GetAwaiter().GetResult();
        output.Seek(0);
        using var image = Image.FromStream(output.AsStream());
        return ImageLoader.Copy(image);
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

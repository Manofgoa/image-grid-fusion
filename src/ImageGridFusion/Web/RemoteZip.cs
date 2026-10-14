using System.Buffers.Binary;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace ImageGridFusion.Web;

/// <summary>One file of a remote zip: where its bytes stand and how they are stored.</summary>
internal sealed record RemoteZipEntry(string Name, ushort Method, long CompressedSize, long Size, long LocalHeaderOffset);

/// <summary>
/// Reads single files out of a zip on a web server without downloading the whole zip — a NuGet package
/// holding every platform's DLLs, of which one is wanted: its central directory, then each entry's
/// bytes alone, fetched with HTTP range requests. Stored and deflated entries, zip64 included. See
/// workfiles/20261013-complementary-ocr-passes.md § RapidOCR Download.
/// </summary>
internal static class RemoteZip
{
    private const uint EndOfDirectorySignature = 0x06054b50;
    private const uint Zip64LocatorSignature = 0x07064b50;
    private const uint Zip64EndSignature = 0x06064b50;
    private const uint DirectoryEntrySignature = 0x02014b50;
    private const uint LocalHeaderSignature = 0x04034b50;

    // The end-of-directory record and its longest comment, then the zip64 locator before it.
    private const int TailLength = 22 + ushort.MaxValue + 20;

    private const int BufferSize = 81920;

    /// <summary>The zip's entries, by name. Throws a <see cref="UrlLoadException"/> when the server or the zip does not allow it.</summary>
    public static async Task<IReadOnlyDictionary<string, RemoteZipEntry>> ReadDirectoryAsync(Uri url, CancellationToken token)
    {
        var (tail, tailStart) = await ReadTailAsync(url, token);
        int end = -1;
        for (int i = tail.Length - 22; i >= 0; i--)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(i)) == EndOfDirectorySignature)
            {
                end = i;
                break;
            }
        }

        if (end < 0)
        {
            throw new UrlLoadException($"{url.Host} did not send a zip");
        }

        long directorySize = BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(end + 12));
        long directoryOffset = BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(end + 16));
        if ((directorySize == uint.MaxValue || directoryOffset == uint.MaxValue) && end >= 20
            && BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(end - 20)) == Zip64LocatorSignature)
        {
            long zip64End = (long)BinaryPrimitives.ReadUInt64LittleEndian(tail.AsSpan(end - 20 + 8));
            byte[] record = await ReadRangeAsync(url, zip64End, 56, token);
            if (BinaryPrimitives.ReadUInt32LittleEndian(record) != Zip64EndSignature)
            {
                throw new UrlLoadException($"{url.Host} sent a damaged zip");
            }

            directorySize = (long)BinaryPrimitives.ReadUInt64LittleEndian(record.AsSpan(40));
            directoryOffset = (long)BinaryPrimitives.ReadUInt64LittleEndian(record.AsSpan(48));
        }

        // The directory may already be in the tail read.
        byte[] directory = directoryOffset >= tailStart && directoryOffset + directorySize <= tailStart + tail.Length
            ? tail.AsSpan((int)(directoryOffset - tailStart), (int)directorySize).ToArray()
            : await ReadRangeAsync(url, directoryOffset, directorySize, token);
        return ParseDirectory(directory, url);
    }

    /// <summary>
    /// Writes <paramref name="entry"/>'s content to <paramref name="target"/>, reporting the compressed
    /// bytes received so far. Throws a <see cref="UrlLoadException"/> when it cannot.
    /// </summary>
    public static async Task ExtractAsync(Uri url, RemoteZipEntry entry, Stream target, IProgress<long> received, CancellationToken token)
    {
        byte[] header = await ReadRangeAsync(url, entry.LocalHeaderOffset, 30, token);
        if (BinaryPrimitives.ReadUInt32LittleEndian(header) != LocalHeaderSignature)
        {
            throw new UrlLoadException($"{url.Host} sent a damaged zip");
        }

        long data = entry.LocalHeaderOffset + 30 + BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(26)) + BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(28));
        using var response = await SendRangeAsync(url, data, entry.CompressedSize, token);
        await using var body = await response.Content.ReadAsStreamAsync(token);
        await using var counted = new CountingStream(body, received);
        await using Stream content = entry.Method switch
        {
            0 => counted,
            8 => new DeflateStream(counted, CompressionMode.Decompress),
            _ => throw new UrlLoadException($"{entry.Name}: unsupported compression {entry.Method}"),
        };
        await content.CopyToAsync(target, BufferSize, token);
    }

    private static IReadOnlyDictionary<string, RemoteZipEntry> ParseDirectory(byte[] directory, Uri url)
    {
        var entries = new Dictionary<string, RemoteZipEntry>(StringComparer.Ordinal);
        int at = 0;
        while (at + 46 <= directory.Length && BinaryPrimitives.ReadUInt32LittleEndian(directory.AsSpan(at)) == DirectoryEntrySignature)
        {
            var span = directory.AsSpan(at);
            ushort method = BinaryPrimitives.ReadUInt16LittleEndian(span[10..]);
            long compressed = BinaryPrimitives.ReadUInt32LittleEndian(span[20..]);
            long size = BinaryPrimitives.ReadUInt32LittleEndian(span[24..]);
            int nameLength = BinaryPrimitives.ReadUInt16LittleEndian(span[28..]);
            int extraLength = BinaryPrimitives.ReadUInt16LittleEndian(span[30..]);
            int commentLength = BinaryPrimitives.ReadUInt16LittleEndian(span[32..]);
            long offset = BinaryPrimitives.ReadUInt32LittleEndian(span[42..]);
            if (at + 46 + nameLength + extraLength > directory.Length)
            {
                break;
            }

            string name = Encoding.UTF8.GetString(span.Slice(46, nameLength));

            // Zip64: the values too large for 32 bits follow in the extra field, in this order.
            var extra = span.Slice(46 + nameLength, extraLength);
            while (extra.Length >= 4)
            {
                ushort id = BinaryPrimitives.ReadUInt16LittleEndian(extra);
                int length = Math.Min(BinaryPrimitives.ReadUInt16LittleEndian(extra[2..]), extra.Length - 4);
                if (id == 1)
                {
                    ReadOnlySpan<byte> values = extra.Slice(4, length);
                    size = Wide(size, ref values);
                    compressed = Wide(compressed, ref values);
                    offset = Wide(offset, ref values);
                }

                extra = extra[(4 + length)..];
            }

            entries[name] = new RemoteZipEntry(name, method, compressed, size, offset);
            at += 46 + nameLength + extraLength + commentLength;
        }

        return entries.Count > 0 ? entries : throw new UrlLoadException($"{url.Host} sent a damaged zip");
    }

    /// <summary>A 32-bit value, or its 64-bit form taken from the zip64 extra field when it overflowed.</summary>
    private static long Wide(long value, ref ReadOnlySpan<byte> values)
    {
        if (value != uint.MaxValue || values.Length < 8)
        {
            return value;
        }

        long wide = (long)BinaryPrimitives.ReadUInt64LittleEndian(values);
        values = values[8..];
        return wide;
    }

    /// <summary>The zip's last bytes — where its end-of-directory record stands — and where they start.</summary>
    private static async Task<(byte[] Tail, long Start)> ReadTailAsync(Uri url, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Range = new RangeHeaderValue(null, TailLength);
        using var response = await SendAsync(request, url, token);
        byte[] tail = await response.Content.ReadAsByteArrayAsync(token);
        long start = response.Content.Headers.ContentRange is { From: { } from } ? from : 0;
        return (tail, start);
    }

    private static async Task<byte[]> ReadRangeAsync(Uri url, long from, long length, CancellationToken token)
    {
        using var response = await SendRangeAsync(url, from, length, token);
        byte[] bytes = await response.Content.ReadAsByteArrayAsync(token);
        return bytes.Length == length ? bytes : throw new UrlLoadException($"{url.Host} sent a short answer");
    }

    private static async Task<HttpResponseMessage> SendRangeAsync(Uri url, long from, long length, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Range = new RangeHeaderValue(from, from + length - 1);
        return await SendAsync(request, url, token);
    }

    /// <summary>The partial answer to a range request; a server ignoring the range is refused rather than downloaded whole.</summary>
    private static async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, Uri url, CancellationToken token)
    {
        HttpResponseMessage response;
        try
        {
            response = await Downloader.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        }
        catch (HttpRequestException e)
        {
            throw new UrlLoadException($"Couldn't reach {url.Host}: {e.Message}", e);
        }
        catch (TaskCanceledException e) when (!token.IsCancellationRequested)
        {
            throw new UrlLoadException($"{url.Host} didn't answer in time", e);
        }

        if (response.StatusCode != HttpStatusCode.PartialContent)
        {
            response.Dispose();
            throw response.IsSuccessStatusCode
                ? new UrlLoadException($"{url.Host} does not send parts of a file")
                : new UrlLoadException($"{url.Host} answered {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        return response;
    }

    /// <summary>A read-only stream reporting the bytes read through it.</summary>
    private sealed class CountingStream(Stream inner, IProgress<long> received) : Stream
    {
        private long _count;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => this._count;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => this.Counted(inner.Read(buffer, offset, count));

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            this.Counted(await inner.ReadAsync(buffer, cancellationToken));

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            this.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private int Counted(int read)
        {
            this._count += read;
            received.Report(this._count);
            return read;
        }
    }
}

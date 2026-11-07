namespace ImageGridFusion.Composition;

/// <summary>
/// The worst-case size of the file Copy / Save produce, computed without encoding anything — what the output
/// readout shows marked <c>≈</c> (RULES.md § Output Readout, workfiles/20261107-status-bar-file-size.md § Estimate).
/// The video's bitrates are the encoder's own, read from here by <see cref="Imaging.VideoEncoder"/>.
/// </summary>
internal static class OutputEstimate
{
    /// <summary>The AAC track's bitrate, in bytes a second.</summary>
    public const int AacBytesPerSecond = 24000;

    /// <summary>The H.264 bitrate's floor and ceiling, in bits a second.</summary>
    public const long MinVideoBitrate = 2_000_000;

    public const long MaxVideoBitrate = 40_000_000;

    /// <summary>The bits per pixel and frame the H.264 bitrate aims at: enough for sharp stills and text at 30 fps.</summary>
    private const double BitsPerPixelAndFrame = 0.12;

    /// <summary>Deflate's stored blocks hold at most this many bytes, each behind a 5-byte header.</summary>
    private const int StoredBlock = 65535;

    /// <summary>The smallest IDAT chunk a PNG writer is assumed to cut, each costing 12 bytes of chunk header and CRC.</summary>
    private const int MinIdatChunk = 8192;

    /// <summary>The PNG's fixed parts — signature, IHDR, IEND, the zlib header and checksum — and room for ancillary chunks.</summary>
    private const long PngFixedBytes = 1024;

    /// <summary>The MP4 container's share over its tracks, an upper bound: its index and headers.</summary>
    private const double ContainerShare = 0.03;

    /// <summary>The MP4 container's fixed part, its headers whatever the length.</summary>
    private const long ContainerFixedBytes = 64 * 1024;

    /// <summary>The H.264 bitrate the encoder asks for at <paramref name="size"/>, in bits a second.</summary>
    public static long VideoBitrate(Size size) =>
        Math.Clamp((long)(size.Width * (double)size.Height * Animation.FramesPerSecond * BitsPerPixelAndFrame), MinVideoBitrate, MaxVideoBitrate);

    /// <summary>
    /// The PNG at <paramref name="size"/> stored uncompressed — 32 bits a pixel, one filter byte a row, in deflate's
    /// stored blocks and the writer's IDAT chunks — the most a PNG of that size can weigh.
    /// </summary>
    public static long PngUpperBound(Size size)
    {
        if (size.Width <= 0 || size.Height <= 0)
        {
            return 0;
        }

        long raw = size.Height * (1 + 4L * size.Width);
        long blocks = (raw + StoredBlock - 1) / StoredBlock;
        long deflated = raw + 5 * blocks;
        long chunks = (deflated + MinIdatChunk - 1) / MinIdatChunk;
        return deflated + 12 * chunks + PngFixedBytes;
    }

    /// <summary>
    /// The MP4 at <paramref name="size"/> lasting <paramref name="length"/>: its video at the encoder's bitrate, its
    /// sound track when <paramref name="sound"/>, its container's share — 0 without a length.
    /// </summary>
    public static long Mp4UpperBound(Size size, TimeSpan length, bool sound)
    {
        if (length <= TimeSpan.Zero || size.Width <= 0 || size.Height <= 0)
        {
            return 0;
        }

        double perSecond = VideoBitrate(size) / 8d + (sound ? AacBytesPerSecond : 0);
        return (long)Math.Ceiling(perSecond * length.TotalSeconds * (1 + ContainerShare)) + ContainerFixedBytes;
    }
}

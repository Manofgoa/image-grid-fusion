namespace ImageGridFusion.Web;

/// <summary>Where a URL's load stands, reported to the URL zone as it goes.</summary>
internal abstract record UrlLoadStep;

/// <summary>The resolvers are looking for the media behind the URL.</summary>
internal sealed record ResolvingStep : UrlLoadStep;

/// <summary>A picture of the media, known before it is downloaded (an X post's poster).</summary>
internal sealed record PosterStep(Uri Url) : UrlLoadStep;

/// <summary>The media is downloading: what is received, of a total the server may not give.</summary>
internal sealed record DownloadingStep(long Received, long? Total, double? Percent = null) : UrlLoadStep;

/// <summary>A tool — yt-dlp, its companions — is being installed with winget.</summary>
internal sealed record InstallingStep(string Name) : UrlLoadStep;

/// <summary>yt-dlp loads the URL, <paramref name="CompanionsMissing"/> when Deno or ffmpeg is not there.</summary>
internal sealed record YtDlpStep(bool CompanionsMissing) : UrlLoadStep;

/// <summary>ffmpeg is joining the video track and its sound.</summary>
internal sealed record MergingStep : UrlLoadStep;

/// <summary>A load that failed, its message said as is in the URL zone.</summary>
internal class UrlLoadException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>The URL needs yt-dlp, and it is not installed: the URL zone offers to install it.</summary>
internal sealed class YtDlpMissingException() : UrlLoadException("This site needs yt-dlp");

/// <summary>yt-dlp failed while one of its companions is missing: the URL zone offers to install them.</summary>
internal sealed class YtDlpCompanionsMissingException(string message) : UrlLoadException(message);

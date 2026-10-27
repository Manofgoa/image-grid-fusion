using System.Globalization;
using ImageGridFusion.UI;

namespace ImageGridFusion.Web;

/// <summary>
/// yt-dlp, the fallback for the sites the built-in resolvers do not know. Not shipped with the app:
/// looked for in its own folder next to the exe only, and installed there with winget when the user
/// accepts the URL zone's offer. Asked for the largest format up to the Video size — a video track and
/// its sound joined by ffmpeg when its companions are there, the largest single file otherwise.
/// See workfiles/20261009-url-content.md § yt-dlp, workfiles/20261015-yt-dlp-best-format.md.
/// </summary>
internal static class YtDlp
{
    public const string FolderName = "yt-dlp";

    // Its companions are installed on their own, in folders of their own (YtDlpCompanions).
    public static readonly WingetTool Tool = new("yt-dlp", FolderName, "yt-dlp.exe", "yt-dlp.yt-dlp", skipDependencies: true);

    // How often the progress is reported, in ms.
    private const int ReportInterval = 100;

    // The marks starting the lines the templates below print, told from yt-dlp's own lines.
    private const string TracksMark = "[igf-tracks]";
    private const string ProgressMark = "[igf-progress]";

    // At equal resolution, the codecs Windows decodes most surely first — H.264 > VP9 > AV1 — and AAC
    // for the sound; the resolution first, whatever the codec.
    private const string SortOrder = "res,+codec:avc:m4a";

    public static string Folder => Tool.Folder;

    /// <summary>The command <see cref="InstallAsync"/> runs, as the user is shown it before it does.</summary>
    public static string InstallCommand => Tool.InstallCommand;

    /// <summary>yt-dlp.exe at the root of its folder, else the first one below it; null when it is not there.</summary>
    public static string? Find() => Tool.Find();

    /// <summary>Installs yt-dlp into its folder with winget; throws a <see cref="UrlLoadException"/> when it cannot.</summary>
    public static Task InstallAsync(CancellationToken token) => Tool.InstallAsync(token);

    /// <summary>
    /// yt-dlp's format selection: the largest format whose longer side is at most <paramref name="maxSide"/>
    /// (a size unknown passes), the largest of all when none is — with ffmpeg (<paramref name="merge"/>), the
    /// best video track plus the best sound, else the best single file.
    /// </summary>
    public static string FormatSelector(int maxSide, bool merge)
    {
        string capped = $"[width<=?{maxSide}][height<=?{maxSide}]";
        return merge
            ? $"bv*{capped}+ba/b{capped}/bv*+ba/b"
            : $"b{capped}/b";
    }

    /// <summary>
    /// What yt-dlp downloads for <paramref name="url"/>; returns its path. Throws a
    /// <see cref="YtDlpMissingException"/> when yt-dlp is not installed, a
    /// <see cref="YtDlpCompanionsMissingException"/> when it fails while a companion is missing, a
    /// <see cref="UrlLoadException"/> with its error otherwise.
    /// </summary>
    public static async Task<string> LoadAsync(Uri url, IProgress<UrlLoadStep> progress, CancellationToken token)
    {
        string exe = Find() ?? throw new YtDlpMissingException();
        string? deno = YtDlpCompanions.Deno.Find();
        string? ffmpeg = YtDlpCompanions.Ffmpeg.Find();
        bool companionsMissing = deno is null || ffmpeg is null;
        progress.Report(new YtDlpStep(companionsMissing));

        Directory.CreateDirectory(UrlDownloads.Folder);
        var info = WingetTool.Hidden(exe);
        info.Environment["NO_COLOR"] = "1";
        info.Environment["PYTHONIOENCODING"] = "utf-8";
        foreach (string argument in Arguments(url, deno, ffmpeg))
        {
            info.ArgumentList.Add(argument);
        }

        string? file = null;
        string? error = null;
        var gauge = new TrackGauge();
        long reported = 0;
        bool merging = false;
        void Read(string? line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            line = line.Trim();
            if (line.StartsWith("ERROR:", StringComparison.Ordinal))
            {
                error = line["ERROR:".Length..].Trim();
            }
            else if (line.StartsWith(TracksMark, StringComparison.Ordinal))
            {
                gauge.SetTracks(line[TracksMark.Length..]);
            }
            else if (line.StartsWith(ProgressMark, StringComparison.Ordinal))
            {
                if (gauge.Read(line[ProgressMark.Length..]) is not { } percent)
                {
                    return;
                }

                if (gauge.Merging && !merging)
                {
                    merging = true;
                    progress.Report(new MergingStep());
                }
                else if (!merging && (Environment.TickCount64 - Interlocked.Read(ref reported) >= ReportInterval || percent >= 100))
                {
                    Interlocked.Exchange(ref reported, Environment.TickCount64);
                    progress.Report(new DownloadingStep(0, null, percent));
                }
            }
            else if (!line.StartsWith('[') && Path.IsPathFullyQualified(line) && File.Exists(line))
            {
                file = line;
            }
        }

        int exitCode = await WingetTool.RunAsync(info, Read, token);
        if (file is not null)
        {
            return file;
        }

        string message = error is null ? $"yt-dlp failed (exit code {exitCode})" : $"yt-dlp: {error}";
        throw companionsMissing ? new YtDlpCompanionsMissingException(message) : new UrlLoadException(message);
    }

    /// <summary>yt-dlp's arguments: the format, the companions there, the templates <see cref="LoadAsync"/> reads.</summary>
    private static IEnumerable<string> Arguments(Uri url, string? deno, string? ffmpeg)
    {
        string[] common =
        [
            "--no-playlist", "--newline", "--progress", "--encoding", "utf-8",
            "--print", "after_move:filepath",
            // The tracks: their format ids ("137+140") and their sizes, for one gauge over all of them.
            "--print", $"before_dl:{TracksMark} %(format_id)s %(requested_formats.0.filesize,requested_formats.0.filesize_approx,filesize,filesize_approx)s %(requested_formats.1.filesize,requested_formats.1.filesize_approx)s",
            "--progress-template", $"download:{ProgressMark} %(progress.status)s %(info.format_id)s %(progress.downloaded_bytes)s %(progress.total_bytes,progress.total_bytes_estimate)s",
            "-S", SortOrder,
            "-f", FormatSelector(AppSettings.VideoMaxSide, merge: ffmpeg is not null),
            "-o", Path.Combine(UrlDownloads.Folder, "%(extractor)s-%(id)s%(height&-{}p|)s.%(ext)s"),
            // Deno only from its own folder: never one on the PATH.
            "--no-js-runtimes",
        ];
        foreach (string argument in common)
        {
            yield return argument;
        }

        if (deno is not null)
        {
            yield return "--js-runtimes";
            yield return $"deno:{deno}";
        }

        if (ffmpeg is not null)
        {
            yield return "--ffmpeg-location";
            yield return Path.GetDirectoryName(ffmpeg)!;
            yield return "--merge-output-format";
            yield return "mp4";
        }

        yield return "--";
        yield return url.AbsoluteUri;
    }

    /// <summary>
    /// One gauge over every track yt-dlp downloads — a video and its sound, one after the other — each
    /// weighted by its size when yt-dlp knows them all, by an equal share otherwise; never going back.
    /// </summary>
    private sealed class TrackGauge
    {
        private string[] _ids = [];
        private long?[] _sizes = [];
        private string? _current;
        private int _index = -1;
        private double _shown;

        /// <summary>The last track is downloaded and more than one were: ffmpeg is joining them.</summary>
        public bool Merging { get; private set; }

        /// <summary>The tracks line: "137+140 12345678 2345678", a size NA when unknown.</summary>
        public void SetTracks(string text)
        {
            string[] parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return;
            }

            this._ids = parts[0].Split('+');
            this._sizes = [.. Enumerable.Range(0, this._ids.Length).Select(i => i + 1 < parts.Length ? Bytes(parts[i + 1]) : null)];
        }

        /// <summary>A progress line: "downloading 137 1234 98765"; the whole load's percentage, or null when unreadable.</summary>
        public double? Read(string text)
        {
            string[] parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4 || Bytes(parts[2]) is not { } received)
            {
                return null;
            }

            string id = parts[1];
            if (id != this._current)
            {
                this._current = id;
                int known = Array.IndexOf(this._ids, id);
                this._index = known >= 0 ? known : this._index + 1;
            }

            int count = Math.Max(this._ids.Length, this._index + 1);
            long? total = Bytes(parts[3]);
            double share = total is > 0 ? Math.Min(1, (double)received / total.Value) : 0;
            double percent;
            if (this._sizes.Length == count && this._sizes.All(s => s is > 0))
            {
                long before = this._sizes.Take(this._index).Sum(s => s!.Value);
                long all = this._sizes.Sum(s => s!.Value);
                percent = 100.0 * (before + share * this._sizes[this._index]!.Value) / all;
            }
            else
            {
                percent = 100.0 * (this._index + share) / count;
            }

            if (parts[0] == "finished" && this._index == count - 1)
            {
                percent = 100;
                this.Merging = count > 1;
            }

            this._shown = Math.Max(this._shown, Math.Min(100, percent));
            return this._shown;
        }

        private static long? Bytes(string text) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && value >= 0 ? (long)value : null;
    }
}

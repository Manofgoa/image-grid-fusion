using System.Diagnostics;
using System.Drawing.Imaging;
using ImageGridFusion.Composition;

namespace ImageGridFusion.Imaging;

/// <summary>
/// Exports a grid holding animated content: as an MP4 video or a looping GIF of every source playing
/// from the starting point of its frames effect, a frozen one showing its frame; or, with nothing
/// playing, as a still of the page each source shows. A cell whose Animations effect is on moves in the
/// video, from its starting state at the first frame, and shows that state in a still. With a
/// soundtrack, a grid of stills is exported as an MP4 video as long as it. With the Cascade on, the
/// videos and animated GIFs play their turns one after the other (<see cref="CascadeSchedule"/>).
/// </summary>
internal static class GridExport
{
    /// <summary>
    /// What to export, captured on the UI thread: still images copied, animated sources referenced —
    /// the grid is locked while an export runs, so they stay alive.
    /// </summary>
    public sealed class Job : IDisposable
    {
        private Job(IReadOnlyList<Item> items, int cellCount, IReadOnlyList<LayerPlacement> layerPlaces, GridLayout? layout, double ratio, GridBorders? borders, IReadOnlyList<SourceImage> heard, Soundtrack? soundtrack, SoundFade? fade, SeamFade? seams, CascadeSchedule? cascade, int videoMaxSide, int stillMaxSide)
        {
            Items = items;
            this.CellCount = cellCount;
            this.LayerPlaces = layerPlaces;
            Layout = layout;
            Ratio = ratio;
            Borders = borders;
            Fade = fade;
            this.Seams = seams;
            this.Cascade = cascade;
            this.VideoMaxSide = videoMaxSide;
            this.StillMaxSide = stillMaxSide;

            // In the cascade, a content taking part counts by its turn, the others by their loops beside it.
            var beside = items
                .Select((item, i) => Animation.LoopOf(cascade?.Has(i) == true ? TimeSpan.Zero : item.Loop, item.Look))
                .DefaultIfEmpty(TimeSpan.Zero)
                .Max();
            var grid = cascade is not null && cascade.Length > beside ? cascade.Length : beside;
            Length = Animation.VideoLength(grid, soundtrack);

            // The soundtrack loops on its own length, cut where the video ends; in the cascade, a video is
            // heard during its turn only.
            var sounds = heard.Select(i =>
            {
                int index = items.ToList().FindIndex(item => item.Image == i);
                return cascade is not null && cascade.Has(index)
                    ? new MixedSound(i.FilePath!, i.PlayedLength, i.StartTime, i.Look.SoundGain, i.PlayedFrom, cascade.TurnAt(index), cascade.Length)
                    : new MixedSound(i.FilePath!, i.PlayedLength, i.StartTime, i.Look.SoundGain, i.PlayedFrom);
            });
            Sounds = (soundtrack is null ? sounds : sounds.Append(new MixedSound(soundtrack.Path, soundtrack.Duration, TimeSpan.Zero, soundtrack.Level))).ToList();
        }

        /// <summary>The cells, then the layers over them (<see cref="CellCount"/> tells them apart): every content that plays or shows.</summary>
        public IReadOnlyList<Item> Items { get; }

        /// <summary>How many of the <see cref="Items"/> are cells; the others are the layers, in their stacking order.</summary>
        public int CellCount { get; }

        /// <summary>Where each layer stands on the grid, the first one for item <see cref="CellCount"/>.</summary>
        public IReadOnlyList<LayerPlacement> LayerPlaces { get; }

        /// <summary>The cells' layout; <c>null</c> for a grid holding layers only.</summary>
        public GridLayout? Layout { get; }

        /// <summary>The canvas's width ÷ height, the output format's as the export started — the free one as computed then.</summary>
        public double Ratio { get; }

        /// <summary>The borders drawn on the grid; <c>null</c> while they are off.</summary>
        public GridBorders? Borders { get; }

        /// <summary>The fade of the mixed sounds, in at the start of the video and out at its end; <c>null</c> while it is off.</summary>
        public SoundFade? Fade { get; }

        /// <summary>The seams fading the cells' flat fills into each other; <c>null</c> while they are off.</summary>
        public SeamFade? Seams { get; }

        /// <summary>The turns of the Cascade; <c>null</c> while it is off.</summary>
        public CascadeSchedule? Cascade { get; }

        /// <summary>
        /// The longer side the canvas of a video export is clamped to: the Video size setting as the export
        /// started (workfiles/20261010-optimisation-lack-of-memory.md § 2.3). A still reads <see cref="StillMaxSide"/>.
        /// </summary>
        public int VideoMaxSide { get; }

        /// <summary>
        /// The longer side the canvas of a still is clamped to: <see cref="CanvasSizer.MaxLongSide"/>, or a
        /// PNG max size entry's, or a JPEG for sharing's (workfiles/20261015-png-native-resolution.md).
        /// </summary>
        public int StillMaxSide { get; }

        /// <summary>The sounds mixed into the video: each from the starting point of its video, at its volume; the soundtrack last.</summary>
        public IReadOnlyList<MixedSound> Sounds { get; }

        /// <summary>Length of the video (<see cref="Animation.VideoLength(TimeSpan, Soundtrack?)"/>): the longest loop, motion cycles included; with a soundtrack and no loop, the soundtrack's.</summary>
        public TimeSpan Length { get; }

        /// <summary>
        /// The grid as it stands, at its <paramref name="ratio"/>, with its <paramref name="borders"/>,
        /// <paramref name="soundtrack"/> mixed over its sounds when one is on, the mix faded by
        /// <paramref name="fade"/> when it is on, the cells' fills faded by <paramref name="seams"/>, and the
        /// videos playing one after the other with <paramref name="cascade"/> when it is on; a video export's
        /// canvas clamped to <paramref name="videoMaxSide"/>, a still's to <paramref name="stillMaxSide"/>; the
        /// <paramref name="layers"/> over everything, playing beside the cells, never taking a turn in the cascade.
        /// </summary>
        public static Job Capture(IReadOnlyList<SourceImage> images, GridLayout? layout, double ratio, GridBorders? borders, Soundtrack? soundtrack = null, SoundFade? fade = null, SeamFade? seams = null, VideoCascade? cascade = null, int videoMaxSide = CanvasSizer.MaxSharedLongSide, int stillMaxSide = CanvasSizer.MaxLongSide, GridLayers? layers = null)
        {
            var layered = layers?.Items ?? [];
            var contents = images.Concat(layered.Select(l => l.Image)).ToList();
            return new(
                contents.Select(ItemOf).ToList(),
                images.Count,
                [.. layered.Select(l => l.Place)],
                layout,
                ratio,
                borders,
                Animation.Heard(contents),
                soundtrack,
                fade,
                seams,
                cascade is null ? null : CascadeSchedule.Of(images, cascade),
                videoMaxSide,
                stillMaxSide);
        }

        private static Item ItemOf(SourceImage i) => i.Plays
            ? new Item(null, i.BandColor, i.Pages, i.PlayedLength, i.DrawnLook, i.Page, i.StartTime, i.PlayedFrom, i.Sharpening, i.Sharpens) { Image = i }
            : i.IsAnimated
            ? new Item(null, i.BandColor, i.Pages, TimeSpan.Zero, i.DrawnLook, i.StartPage, TimeSpan.Zero, Sharpening: i.Sharpening, Sharpens: i.Sharpens)
            : new Item(new Bitmap(i.DrawnBitmap), i.BandColor, null, TimeSpan.Zero, i.DrawnLook, 0, TimeSpan.Zero, Sharpening: i.Sharpening, Sharpens: i.Sharpens);

        /// <summary>The cells' part of <paramref name="frames"/>, one per item.</summary>
        public ArraySegment<Frame> Cells(Frame[] frames) => new(frames, 0, this.CellCount);

        /// <summary>The layers' frames, written into <paramref name="layers"/> from <paramref name="frames"/>, one per item.</summary>
        public void Layers(Frame[] frames, LayerFrame[] layers)
        {
            for (int i = 0; i < layers.Length; i++)
            {
                layers[i] = new LayerFrame(frames[this.CellCount + i], this.LayerPlaces[i]);
            }
        }

        /// <summary>
        /// Content time the cell at <paramref name="index"/> shows at <paramref name="time"/> of the video:
        /// where its turn of the cascade has brought it, else where its own loop has.
        /// </summary>
        public TimeSpan ContentTime(int index, TimeSpan time)
        {
            var item = this.Items[index];
            return this.Cascade is { } cascade && cascade.Has(index)
                ? item.ContentTime(cascade.Played(index, time))
                : item.ContentTime(time);
        }

        public void Dispose()
        {
            foreach (var item in Items)
            {
                item.Still?.Dispose();
            }
        }
    }

    /// <summary>
    /// A cell: a still copy, or an animated source with its loop, the page it shows and where it
    /// starts playing, and the effects on the image. A frozen source has no loop: its page is a still.
    /// The loop is the part its frames effect leaves to play, beginning at <paramref name="From"/> in
    /// the content (workfiles/20261008-video-trim.md).
    /// </summary>
    public sealed record Item(Bitmap? Still, BandColor BandColor, PageSource? Source, TimeSpan Loop, ImageLook Look, int Page, TimeSpan Start, TimeSpan From = default, double Sharpening = 1, bool Sharpens = false)
    {
        public bool Plays => Source is not null && Loop > TimeSpan.Zero;

        /// <summary>The playing image the cell was captured from, to find its sound; <c>null</c> for a still.</summary>
        public SourceImage? Image { get; init; }

        /// <summary>Content time shown at <paramref name="time"/> of the video.</summary>
        public TimeSpan ContentTime(TimeSpan time) => this.From + Animation.LoopTime(this.Start + time, this.Loop);
    }

    /// <summary>
    /// The first frame of a cell: its still, the frame at its starting point, or its frozen page —
    /// owned by the caller unless it is the still. Opens the reader of a playing source.
    /// </summary>
    internal static Frame FirstFrame(Item item, bool play, out AnimationReader? reader)
    {
        reader = null;
        if (item.Source is null)
        {
            return new Frame(item.Still!, item.BandColor, item.Look, Sharpening: item.Sharpening, Sharpens: item.Sharpens);
        }

        if (!play || !item.Plays)
        {
            var page = item.Source.Render(item.Page);
            try
            {
                return new Frame(page, BandColor.Of(page), item.Look, Sharpening: item.Sharpening, Sharpens: item.Sharpens);
            }
            catch
            {
                // Its band color failing, the page is disposed rather than leaked.
                page.Dispose();
                throw;
            }
        }

        reader = item.Source.OpenAnimation();
        var first = reader.FrameAt(item.ContentTime(TimeSpan.Zero)) ?? throw new InvalidOperationException("A source has no frame to show.");
        return new Frame(first, item.BandColor, item.Look, Sharpening: item.Sharpening, Sharpens: item.Sharpens);
    }

    /// <summary>The file an animated export writes.</summary>
    public enum Format
    {
        /// <summary>H.264 video with the mixed sounds.</summary>
        Mp4,

        /// <summary>Animated GIF looping forever, silent.</summary>
        Gif,
    }

    /// <summary>
    /// What an export produced: the files whose sound was mixed in, the ones left out, their sound not
    /// re-encodable, and the most private memory the process held while it ran, sampled every few frames
    /// (<see cref="MemoryProbe"/>; 0 for a still).
    /// </summary>
    public sealed record Result(Size Size, TimeSpan Length, int Frames, IReadOnlyList<string> MixedSounds, IReadOnlyList<string> FailedSounds, long PeakPrivateBytes = 0);

    /// <summary>
    /// Writes the animation to <paramref name="path"/> in <paramref name="format"/>: 30 fps, as long as
    /// the longest loop, the others starting over — or, for a grid of stills with a soundtrack, as long
    /// as the soundtrack. The canvas is sized once, from the frames at the
    /// start. Blocks: meant to run off the UI thread. Deletes the incomplete file when cancelled or failing.
    /// With <paramref name="logPath"/>, the memory probe logs there what the export holds as it goes.
    /// </summary>
    public static Result RenderAnimation(Job job, Format format, string path, IProgress<double>? progress, CancellationToken cancellation, string? logPath = null)
    {
        var readers = new AnimationReader?[job.Items.Count];
        var frames = new Frame[job.Items.Count];
        IFrameEncoder? encoder = null;
        bool finished = false;
        using var probe = new MemoryProbe(logPath);
        try
        {
            for (int i = 0; i < job.Items.Count; i++)
            {
                frames[i] = FirstFrame(job.Items[i], play: true, out readers[i]);
            }

            probe.Sample(0, null, "readers open");

            // The layers never size the canvas; their frames are rewritten into this array at each frame.
            var layers = new LayerFrame[job.LayerPlaces.Count];
            job.Layers(frames, layers);
            var canvas = Animation.EvenSize(job.Layout is null
                ? CanvasSizer.ForLayers(layers, job.Ratio, job.VideoMaxSide)
                : CanvasSizer.Compute(job.Cells(frames).Select(f => f.Sizing).ToList(), job.Layout, job.Ratio, job.Borders, job.VideoMaxSide));
            using var bitmap = new Bitmap(canvas.Width, canvas.Height, PixelFormat.Format32bppRgb);
            using var g = Graphics.FromImage(bitmap);
            encoder = format == Format.Gif
                ? GifEncoder.Create(path, canvas)
                : VideoEncoder.Create(path, canvas, job.Length, job.Sounds, job.Fade);
            probe.Sample(0, encoder, $"encoder created, canvas {canvas.Width} x {canvas.Height}");

            int count = Animation.FrameCount(job.Length);
            for (int k = 0; k < count; k++)
            {
                cancellation.ThrowIfCancellationRequested();
                var time = Animation.FrameTime(k);
                for (int i = 0; k > 0 && i < readers.Length; i++)
                {
                    if (readers[i]?.FrameAt(job.ContentTime(i, time)) is { } next)
                    {
                        frames[i].Bitmap.Dispose();
                        frames[i] = frames[i] with { Bitmap = next };
                    }
                }

                // The motions of the Animations effects, on the video's clock.
                for (int i = 0; i < frames.Length; i++)
                {
                    frames[i] = frames[i] with { Time = time };
                }

                // Neither format keeps an alpha channel: a cell without background is flattened on white,
                // cleared at each frame so the previous one does not show through.
                g.Clear(Color.White);
                job.Layers(frames, layers);
                Compositor.Draw(g, job.Cells(frames), job.Layout, canvas, job.Borders, job.Seams, layers: layers);
                encoder.WriteFrame(bitmap, time, Animation.FrameTime(k + 1) - time);
                if ((k + 1) % MemoryProbe.EveryFrames == 0)
                {
                    probe.Sample(k + 1, encoder);
                }

                progress?.Report((k + 1) / (double)count);
            }

            encoder.Finish();
            probe.Sample(count, encoder, "finished");
            finished = true;
            return encoder is VideoEncoder video
                ? new Result(canvas, job.Length, count, video.MixedSounds, video.FailedSounds, probe.PeakPrivateBytes)
                : new Result(canvas, job.Length, count, [], [], probe.PeakPrivateBytes);
        }
        finally
        {
            encoder?.Dispose();
            for (int i = 0; i < readers.Length; i++)
            {
                readers[i]?.Dispose();
                if (job.Items[i].Source is not null)
                {
                    frames[i].Bitmap?.Dispose();
                }
            }

            if (!finished)
            {
                TryDelete(path);
            }
        }
    }

    /// <summary>
    /// Renders the grid as a still, each animated source showing the page it shows in the preview.
    /// Blocks: meant to run off the UI thread.
    /// </summary>
    public static Bitmap RenderStill(Job job)
    {
        var frames = new Frame[job.Items.Count];
        var owned = new List<Bitmap>();
        try
        {
            for (int i = 0; i < job.Items.Count; i++)
            {
                var item = job.Items[i];
                if (item.Source is null)
                {
                    frames[i] = new Frame(item.Still!, item.BandColor, item.Look, Sharpening: item.Sharpening, Sharpens: item.Sharpens);
                    continue;
                }

                var frame = item.Source.Render(item.Page);
                owned.Add(frame);
                frames[i] = new Frame(frame, BandColor.Of(frame), item.Look, Sharpening: item.Sharpening, Sharpens: item.Sharpens);
            }

            var layers = new LayerFrame[job.LayerPlaces.Count];
            job.Layers(frames, layers);
            return Compositor.Render(job.Cells(frames), job.Layout, job.Ratio, job.Borders, job.Seams, job.StillMaxSide, layers);
        }
        finally
        {
            owned.ForEach(b => b.Dispose());
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Left behind: nothing more to do.
        }
    }

    /// <summary>
    /// Watches the process's memory during an animated export (workfiles/20261010-optimisation-lack-of-memory.md
    /// § 3): the peak goes into the result; given a log file, a line every <see cref="EveryFrames"/> frames —
    /// the frame, the private bytes, the managed heap, the writer's statistics — says which holder
    /// dominates: received − encoded climbing, the writer was queueing; private bytes high but flat, with
    /// a jump when the readers open, the per-video readers; climbing with a flat gap, a leak elsewhere.
    /// </summary>
    private sealed class MemoryProbe : IDisposable
    {
        public const int EveryFrames = 30;

        private readonly StreamWriter? _log;

        public MemoryProbe(string? logPath)
        {
            if (logPath is null)
            {
                return;
            }

            try
            {
                this._log = new StreamWriter(logPath, append: false);
                this._log.WriteLine("time\tframe\tprivate bytes\tmanaged bytes\treceived\tencoded\tprocessed\tqueued bytes\tsink requests\tnote");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // No log: the export goes on without it.
            }
        }

        /// <summary>The most private memory the process held at a sample.</summary>
        public long PeakPrivateBytes { get; private set; }

        /// <summary>Samples the memory at <paramref name="frame"/>, the writer's statistics read from <paramref name="encoder"/> when it is the video one.</summary>
        public void Sample(int frame, IFrameEncoder? encoder, string note = "")
        {
            using var process = Process.GetCurrentProcess();
            long privateBytes = process.PrivateMemorySize64;
            this.PeakPrivateBytes = Math.Max(this.PeakPrivateBytes, privateBytes);
            if (this._log is null)
            {
                return;
            }

            var statistics = (encoder as VideoEncoder)?.VideoStatistics;
            try
            {
                this._log.WriteLine(string.Join(
                    '\t',
                    DateTime.Now.ToString("HH:mm:ss.fff"),
                    frame,
                    privateBytes,
                    GC.GetTotalMemory(forceFullCollection: false),
                    statistics?.SamplesReceived.ToString() ?? "",
                    statistics?.SamplesEncoded.ToString() ?? "",
                    statistics?.SamplesProcessed.ToString() ?? "",
                    statistics?.ByteCountQueued.ToString() ?? "",
                    statistics?.OutstandingSinkSampleRequests.ToString() ?? "",
                    note));
                this._log.Flush();
            }
            catch (IOException)
            {
                // A line lost: the export goes on.
            }
        }

        public void Dispose() => this._log?.Dispose();
    }
}

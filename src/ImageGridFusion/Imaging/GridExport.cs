using System.Drawing.Imaging;
using ImageGridFusion.Composition;

namespace ImageGridFusion.Imaging;

/// <summary>
/// Exports a grid holding animated content: as an MP4 video or a looping GIF of every source playing
/// from the starting point of its frames effect, a frozen one showing its frame; or, with nothing
/// playing, as a still of the page each source shows. A cell whose Animations effect is on moves in the
/// video, from its starting state at the first frame, and shows that state in a still. With a
/// soundtrack, a grid of stills is exported as an MP4 video as long as it.
/// </summary>
internal static class GridExport
{
    /// <summary>
    /// What to export, captured on the UI thread: still images copied, animated sources referenced —
    /// the grid is locked while an export runs, so they stay alive.
    /// </summary>
    public sealed class Job : IDisposable
    {
        private Job(IReadOnlyList<Item> items, GridLayout layout, double ratio, GridBorders? borders, IReadOnlyList<SourceImage> heard, Soundtrack? soundtrack, SoundFade? fade, SeamFade? seams)
        {
            Items = items;
            Layout = layout;
            Ratio = ratio;
            Borders = borders;
            Fade = fade;
            this.Seams = seams;
            Length = Animation.VideoLength(items.Select(i => Animation.LoopOf(i.Loop, i.Look)).DefaultIfEmpty(TimeSpan.Zero).Max(), soundtrack);

            // The soundtrack loops on its own length, cut where the video ends.
            var sounds = heard.Select(i => new MixedSound(i.FilePath!, i.PlayedLength, i.StartTime, i.Look.SoundGain, i.PlayedFrom));
            Sounds = (soundtrack is null ? sounds : sounds.Append(new MixedSound(soundtrack.Path, soundtrack.Duration, TimeSpan.Zero, soundtrack.Level))).ToList();
        }

        public IReadOnlyList<Item> Items { get; }

        public GridLayout Layout { get; }

        /// <summary>The canvas's width ÷ height, the output format's as the export started — the free one as computed then.</summary>
        public double Ratio { get; }

        /// <summary>The borders drawn on the grid; <c>null</c> while they are off.</summary>
        public GridBorders? Borders { get; }

        /// <summary>The fade of the mixed sounds, in at the start of the video and out at its end; <c>null</c> while it is off.</summary>
        public SoundFade? Fade { get; }

        /// <summary>The seams fading the cells' flat fills into each other; <c>null</c> while they are off.</summary>
        public SeamFade? Seams { get; }

        /// <summary>The sounds mixed into the video: each from the starting point of its video, at its volume; the soundtrack last.</summary>
        public IReadOnlyList<MixedSound> Sounds { get; }

        /// <summary>Length of the video (<see cref="Animation.VideoLength(TimeSpan, Soundtrack?)"/>): the longest loop, motion cycles included; with a soundtrack and no loop, the soundtrack's.</summary>
        public TimeSpan Length { get; }

        /// <summary>
        /// The grid as it stands, at its <paramref name="ratio"/>, with its <paramref name="borders"/>,
        /// <paramref name="soundtrack"/> mixed over its sounds when one is on, the mix faded by
        /// <paramref name="fade"/> when it is on, and the cells' fills faded by <paramref name="seams"/>.
        /// </summary>
        public static Job Capture(IReadOnlyList<SourceImage> images, GridLayout layout, double ratio, GridBorders? borders, Soundtrack? soundtrack = null, SoundFade? fade = null, SeamFade? seams = null) => new(
            images.Select(i => i.Plays
                ? new Item(null, i.BandColor, i.Pages, i.PlayedLength, i.Look, i.Page, i.StartTime, i.PlayedFrom)
                : i.IsAnimated
                ? new Item(null, i.BandColor, i.Pages, TimeSpan.Zero, i.Look, i.StartPage, TimeSpan.Zero)
                : new Item(new Bitmap(i.Bitmap), i.BandColor, null, TimeSpan.Zero, i.Look, 0, TimeSpan.Zero)).ToList(),
            layout,
            ratio,
            borders,
            Animation.Heard(images),
            soundtrack,
            fade,
            seams);

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
    public sealed record Item(Bitmap? Still, BandColor BandColor, PageSource? Source, TimeSpan Loop, ImageLook Look, int Page, TimeSpan Start, TimeSpan From = default)
    {
        public bool Plays => Source is not null && Loop > TimeSpan.Zero;

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
            return new Frame(item.Still!, item.BandColor, item.Look);
        }

        if (!play || !item.Plays)
        {
            var page = item.Source.Render(item.Page);
            return new Frame(page, BandColor.Of(page), item.Look);
        }

        reader = item.Source.OpenAnimation();
        var first = reader.FrameAt(item.ContentTime(TimeSpan.Zero)) ?? throw new InvalidOperationException("A source has no frame to show.");
        return new Frame(first, item.BandColor, item.Look);
    }

    /// <summary>The file an animated export writes.</summary>
    public enum Format
    {
        /// <summary>H.264 video with the mixed sounds.</summary>
        Mp4,

        /// <summary>Animated GIF looping forever, silent.</summary>
        Gif,
    }

    /// <summary>What an export produced: the files whose sound was mixed in, and the ones left out, their sound not re-encodable.</summary>
    public sealed record Result(Size Size, TimeSpan Length, int Frames, IReadOnlyList<string> MixedSounds, IReadOnlyList<string> FailedSounds);

    /// <summary>
    /// Writes the animation to <paramref name="path"/> in <paramref name="format"/>: 30 fps, as long as
    /// the longest loop, the others starting over — or, for a grid of stills with a soundtrack, as long
    /// as the soundtrack. The canvas is sized once, from the frames at the
    /// start. Blocks: meant to run off the UI thread. Deletes the incomplete file when cancelled or failing.
    /// </summary>
    public static Result RenderAnimation(Job job, Format format, string path, IProgress<double>? progress, CancellationToken cancellation)
    {
        var readers = new AnimationReader?[job.Items.Count];
        var frames = new Frame[job.Items.Count];
        IFrameEncoder? encoder = null;
        bool finished = false;
        try
        {
            for (int i = 0; i < job.Items.Count; i++)
            {
                frames[i] = FirstFrame(job.Items[i], play: true, out readers[i]);
            }

            var canvas = Animation.EvenSize(CanvasSizer.Compute(frames.Select(f => f.Size).ToList(), job.Layout, job.Ratio));
            using var bitmap = new Bitmap(canvas.Width, canvas.Height, PixelFormat.Format32bppRgb);
            using var g = Graphics.FromImage(bitmap);
            encoder = format == Format.Gif
                ? GifEncoder.Create(path, canvas)
                : VideoEncoder.Create(path, canvas, job.Length, job.Sounds, job.Fade);

            int count = Animation.FrameCount(job.Length);
            for (int k = 0; k < count; k++)
            {
                cancellation.ThrowIfCancellationRequested();
                var time = Animation.FrameTime(k);
                for (int i = 0; k > 0 && i < readers.Length; i++)
                {
                    if (readers[i]?.FrameAt(job.Items[i].ContentTime(time)) is { } next)
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
                Compositor.Draw(g, frames, job.Layout, canvas, job.Borders, job.Seams);
                encoder.WriteFrame(bitmap, time, Animation.FrameTime(k + 1) - time);
                progress?.Report((k + 1) / (double)count);
            }

            encoder.Finish();
            finished = true;
            return encoder is VideoEncoder video
                ? new Result(canvas, job.Length, count, video.MixedSounds, video.FailedSounds)
                : new Result(canvas, job.Length, count, [], []);
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
                    frames[i] = new Frame(item.Still!, item.BandColor, item.Look);
                    continue;
                }

                var frame = item.Source.Render(item.Page);
                owned.Add(frame);
                frames[i] = new Frame(frame, BandColor.Of(frame), item.Look);
            }

            return Compositor.Render(frames, job.Layout, job.Ratio, job.Borders, job.Seams);
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
}

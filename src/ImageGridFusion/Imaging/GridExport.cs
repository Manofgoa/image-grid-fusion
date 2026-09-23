using System.Drawing.Imaging;
using ImageGridFusion.Composition;

namespace ImageGridFusion.Imaging;

/// <summary>
/// Exports a grid holding animated content: as an MP4 video of every source playing from its start,
/// or, forced to a still, as the page each source shows (the one its slider selects).
/// </summary>
internal static class GridExport
{
    /// <summary>
    /// What to export, captured on the UI thread: still images copied, animated sources referenced —
    /// the grid is locked while an export runs, so they stay alive.
    /// </summary>
    public sealed class Job : IDisposable
    {
        private Job(IReadOnlyList<Item> items, GridLayout layout, double cropThreshold, SourceImage? sound)
        {
            Items = items;
            Layout = layout;
            CropThreshold = cropThreshold;
            SoundPath = sound?.FilePath;
            SoundLoop = sound?.Pages?.LoopDuration ?? TimeSpan.Zero;
            Length = items.Select(i => i.Loop).DefaultIfEmpty(TimeSpan.Zero).Max();
        }

        public IReadOnlyList<Item> Items { get; }

        public GridLayout Layout { get; }

        /// <summary>Crop threshold the preview shows; see <see cref="FitCalculator"/>.</summary>
        public double CropThreshold { get; }

        public string? SoundPath { get; }

        public TimeSpan SoundLoop { get; }

        /// <summary>Length of the video: the longest loop.</summary>
        public TimeSpan Length { get; }

        public static Job Capture(IReadOnlyList<SourceImage> images, GridLayout layout, double cropThreshold) => new(
            images.Select(i => i.IsAnimated
                ? new Item(null, i.BandColor, i.Pages, i.Pages!.LoopDuration, i.Look, i.Page)
                : new Item(new Bitmap(i.Bitmap), i.BandColor, null, TimeSpan.Zero, i.Look, 0)).ToList(),
            layout,
            cropThreshold,
            Animation.SoundSource(images));

        public void Dispose()
        {
            foreach (var item in Items)
            {
                item.Still?.Dispose();
            }
        }
    }

    /// <summary>
    /// A cell: a still copy, or an animated source with its loop and the page it shows, and the actions
    /// on the image.
    /// </summary>
    public sealed record Item(Bitmap? Still, BandColor BandColor, PageSource? Source, TimeSpan Loop, ImageLook Look, int Page);

    /// <summary>What an export produced; <see cref="SoundPath"/> is the source whose sound was written, if any.</summary>
    public sealed record Result(Size Size, TimeSpan Length, int Frames, string? SoundPath, string? SoundProblem);

    /// <summary>
    /// Writes the video to <paramref name="path"/>: 30 fps, as long as the longest loop, the others
    /// starting over. The canvas is sized once, from the frames at the start. Blocks: meant to run off
    /// the UI thread. Deletes the incomplete file when cancelled or failing.
    /// </summary>
    public static Result RenderVideo(Job job, string path, IProgress<double>? progress, CancellationToken cancellation)
    {
        var readers = new AnimationReader?[job.Items.Count];
        var frames = new Frame[job.Items.Count];
        VideoEncoder? encoder = null;
        bool finished = false;
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

                readers[i] = item.Source.OpenAnimation();
                var first = readers[i]!.FrameAt(TimeSpan.Zero) ?? throw new InvalidOperationException("A source has no frame to show.");
                frames[i] = new Frame(first, item.BandColor, item.Look);
            }

            var canvas = Animation.EvenSize(CanvasSizer.Compute(frames.Select(f => f.Size).ToList(), job.Layout, job.CropThreshold));
            using var bitmap = new Bitmap(canvas.Width, canvas.Height, PixelFormat.Format32bppRgb);
            using var g = Graphics.FromImage(bitmap);
            encoder = VideoEncoder.Create(path, canvas, job.Length, job.SoundPath, job.SoundLoop);

            int count = Animation.FrameCount(job.Length);
            for (int k = 0; k < count; k++)
            {
                cancellation.ThrowIfCancellationRequested();
                var time = Animation.FrameTime(k);
                for (int i = 0; k > 0 && i < readers.Length; i++)
                {
                    if (readers[i]?.FrameAt(Animation.LoopTime(time, job.Items[i].Loop)) is { } next)
                    {
                        frames[i].Bitmap.Dispose();
                        frames[i] = frames[i] with { Bitmap = next };
                    }
                }

                Compositor.Draw(g, frames, job.Layout, canvas, job.CropThreshold);
                encoder.WriteFrame(bitmap, time, Animation.FrameTime(k + 1) - time);
                progress?.Report((k + 1) / (double)count);
            }

            encoder.Finish();
            finished = true;
            return new Result(canvas, job.Length, count, encoder.SoundProblem is null ? job.SoundPath : null, encoder.SoundProblem);
        }
        finally
        {
            encoder?.Dispose();
            for (int i = 0; i < readers.Length; i++)
            {
                readers[i]?.Dispose();
                if (readers[i] is not null)
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

            return Compositor.Render(frames, job.Layout, job.CropThreshold);
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

using System.Drawing.Imaging;
using ImageGridFusion.Composition;

namespace ImageGridFusion.Imaging;

/// <summary>
/// Exports the carousel as an MP4 video: whole loops, each arrangement shown for a second (see
/// <see cref="Carousel"/>). Animated contents play while they move, as in the animated export, or,
/// forced to images, stay still on the page each shows, the video then silent.
/// </summary>
internal static class CarouselExport
{
    /// <summary>
    /// Writes the video to <paramref name="path"/>, 30 fps, on a single canvas: the largest over the
    /// arrangements, sized from the frames at the start. With <paramref name="playContents"/>, it lasts
    /// the whole loops that cover the longest content, the shorter ones starting over, and carries the
    /// animated export's sound; else a single loop. Blocks: meant to run off the UI thread. Deletes the
    /// incomplete file when cancelled or failing.
    /// </summary>
    public static GridExport.Result RenderVideo(GridExport.Job job, bool playContents, string path, IProgress<double>? progress, CancellationToken cancellation)
    {
        var readers = new AnimationReader?[job.Items.Count];
        var frames = new Frame[job.Items.Count];
        var owned = new bool[job.Items.Count];
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

                Bitmap first;
                if (playContents)
                {
                    readers[i] = item.Source.OpenAnimation();
                    first = readers[i]!.FrameAt(TimeSpan.Zero) ?? throw new InvalidOperationException("A source has no frame to show.");
                }
                else
                {
                    first = item.Source.Render(item.Page);
                }

                owned[i] = true;
                frames[i] = new Frame(first, item.BandColor, item.Look);
            }

            int steps = frames.Length;
            var length = playContents ? Carousel.Length(steps, job.Length) : Carousel.Length(steps);
            var canvas = Animation.EvenSize(Carousel.CanvasSize(frames.Select(f => f.Size).ToList(), job.Layout, job.CropThreshold));
            using var bitmap = new Bitmap(canvas.Width, canvas.Height, PixelFormat.Format32bppRgb);
            using var g = Graphics.FromImage(bitmap);
            encoder = playContents
                ? VideoEncoder.Create(path, canvas, length, job.SoundPath, job.SoundLoop)
                : VideoEncoder.Create(path, canvas, length, soundPath: null, TimeSpan.Zero);

            // The grid is drawn again only when the arrangement, or a frame shown, changes.
            int count = Animation.FrameCount(length);
            int drawn = -1;
            for (int k = 0; k < count; k++)
            {
                cancellation.ThrowIfCancellationRequested();
                var time = Animation.FrameTime(k);
                bool changed = false;
                for (int i = 0; k > 0 && i < readers.Length; i++)
                {
                    if (readers[i]?.FrameAt(Animation.LoopTime(time, job.Items[i].Loop)) is { } next)
                    {
                        frames[i].Bitmap.Dispose();
                        frames[i] = frames[i] with { Bitmap = next };
                        changed = true;
                    }
                }

                int step = Carousel.StepAt(time, steps);
                if (step != drawn || changed)
                {
                    Compositor.Draw(g, Carousel.Arrange(frames, job.Layout, step), job.Layout, canvas, job.CropThreshold);
                    drawn = step;
                }

                encoder.WriteFrame(bitmap, time, Animation.FrameTime(k + 1) - time);
                progress?.Report((k + 1) / (double)count);
            }

            encoder.Finish();
            finished = true;
            string? sound = playContents && encoder.SoundProblem is null ? job.SoundPath : null;
            return new GridExport.Result(canvas, length, count, sound, encoder.SoundProblem);
        }
        finally
        {
            encoder?.Dispose();
            for (int i = 0; i < frames.Length; i++)
            {
                readers[i]?.Dispose();
                if (owned[i])
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

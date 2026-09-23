using System.Drawing.Imaging;
using ImageGridFusion.Composition;

namespace ImageGridFusion.Imaging;

/// <summary>
/// Exports the carousel as an MP4 video: one full loop, each arrangement shown for a second (see
/// <see cref="Carousel"/>). Every content stays still on the page it shows, so the video is silent.
/// </summary>
internal static class CarouselExport
{
    /// <summary>
    /// Writes the video to <paramref name="path"/>, 30 fps, on a single canvas: the largest over the
    /// arrangements. Blocks: meant to run off the UI thread. Deletes the incomplete file when cancelled
    /// or failing.
    /// </summary>
    public static GridExport.Result RenderVideo(GridExport.Job job, string path, IProgress<double>? progress, CancellationToken cancellation)
    {
        var frames = new Frame[job.Items.Count];
        var owned = new List<Bitmap>();
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

                var page = item.Source.Render(item.Page);
                owned.Add(page);
                frames[i] = new Frame(page, BandColor.Of(page), item.Look);
            }

            int steps = frames.Length;
            var length = Carousel.Length(steps);
            var canvas = Animation.EvenSize(Carousel.CanvasSize(frames.Select(f => f.Size).ToList(), job.Layout, job.CropThreshold));
            using var bitmap = new Bitmap(canvas.Width, canvas.Height, PixelFormat.Format32bppRgb);
            using var g = Graphics.FromImage(bitmap);
            encoder = VideoEncoder.Create(path, canvas, length, soundPath: null, TimeSpan.Zero);

            // An arrangement is drawn once, then written as the frames of its second.
            int count = Animation.FrameCount(length);
            int drawn = -1;
            for (int k = 0; k < count; k++)
            {
                cancellation.ThrowIfCancellationRequested();
                var time = Animation.FrameTime(k);
                int step = Carousel.StepAt(time, steps);
                if (step != drawn)
                {
                    Compositor.Draw(g, Carousel.Arrange(frames, job.Layout, step), job.Layout, canvas, job.CropThreshold);
                    drawn = step;
                }

                encoder.WriteFrame(bitmap, time, Animation.FrameTime(k + 1) - time);
                progress?.Report((k + 1) / (double)count);
            }

            encoder.Finish();
            finished = true;
            return new GridExport.Result(canvas, length, count, SoundPath: null, SoundProblem: null);
        }
        finally
        {
            encoder?.Dispose();
            owned.ForEach(b => b.Dispose());
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

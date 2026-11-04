using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ImageGridFusion.Composition;
using ImageGridFusion.Explorer;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ImageGridFusion.Imaging;

/// <summary>
/// The Upscale's models, from the downloaded engine (<see cref="UpscaleEngineFiles"/>): Real-ESRGAN's RRDBNet ×2 and ×4
/// (Quality) and its compact ×4 (Fast), each loaded the first time it is needed, run in tiles so a large image stays within memory. Used from one thread at a
/// time. See workfiles/20261029-image-upscale.md § Engine.
/// </summary>
internal sealed class UpscaleEngine : IDisposable
{
    // The side of a tile in the image's pixels, and the margin read around it so its seams do not show.
    private const int TileSide = 256;
    private const int TileMargin = 16;

    // A tile's sides are padded up to a multiple of it: the ×2 model folds its input in two before anything else.
    private const int SideMultiple = 4;

    private readonly Dictionary<string, InferenceSession> _sessions = [];

    public void Dispose()
    {
        foreach (var session in this._sessions.Values)
        {
            session.Dispose();
        }

        this._sessions.Clear();
    }

    /// <summary>
    /// <paramref name="source"/> enlarged <paramref name="factor"/> times by <paramref name="model"/>: exactly
    /// <paramref name="factor"/> times its width and height, so the effects' fractions stay exact — the Fast model's ×2
    /// its ×4 halved. The model reads the colors only; the alpha is enlarged on its own, bilinear, and put back.
    /// <paramref name="progress"/> gets the share done after each tile; <paramref name="cancelled"/>, asked between
    /// tiles, stops it with an <see cref="OperationCanceledException"/>.
    /// </summary>
    public Bitmap Upscale(Bitmap source, UpscaleModel model, int factor, Action<double>? progress = null, Func<bool>? cancelled = null)
    {
        int scale = UpscaleEngineFiles.ScaleOf(model, factor);
        var upscaled = this.Run(source, UpscaleEngineFiles.ModelFor(model, factor), scale, progress, cancelled);
        if (scale == factor)
        {
            return upscaled;
        }

        using (upscaled)
        {
            return Reduced(upscaled, source.Width * factor, source.Height * factor);
        }
    }

    // The bitmap halved — or reduced to any size — in high-quality bicubic, its alpha kept.
    private static Bitmap Reduced(Bitmap bitmap, int width, int height)
    {
        var reduced = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        try
        {
            using var g = Graphics.FromImage(reduced);
            using var attributes = new ImageAttributes();
            attributes.SetWrapMode(WrapMode.TileFlipXY);
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(bitmap, new Rectangle(0, 0, width, height), 0, 0, bitmap.Width, bitmap.Height, GraphicsUnit.Pixel, attributes);
            return reduced;
        }
        catch
        {
            reduced.Dispose();
            throw;
        }
    }

    // The file's model run over the image in tiles, its output exactly scale times the image.
    private Bitmap Run(Bitmap source, string file, int factor, Action<double>? progress, Func<bool>? cancelled)
    {
        if (!this._sessions.TryGetValue(file, out var session))
        {
            session = Load(file);
            this._sessions[file] = session;
        }

        int width = source.Width, height = source.Height;
        var pixels = ReadPixels(source);
        int outWidth = width * factor, outHeight = height * factor;
        var output = new byte[(long)outWidth * outHeight * 4];
        string input = session.InputMetadata.Keys.First();
        string name = session.OutputMetadata.Keys.First();
        int columns = (width + TileSide - 1) / TileSide, rows = (height + TileSide - 1) / TileSide;
        int done = 0;
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                if (cancelled?.Invoke() == true)
                {
                    throw new OperationCanceledException();
                }

                // The tile, and the part read around it, clamped to the image.
                int x0 = column * TileSide, y0 = row * TileSide;
                int x1 = Math.Min(width, x0 + TileSide), y1 = Math.Min(height, y0 + TileSide);
                int rx0 = Math.Max(0, x0 - TileMargin), ry0 = Math.Max(0, y0 - TileMargin);
                int rx1 = Math.Min(width, x1 + TileMargin), ry1 = Math.Min(height, y1 + TileMargin);
                int readWidth = Padded(rx1 - rx0), readHeight = Padded(ry1 - ry0);
                var tensor = new DenseTensor<float>([1, 3, readHeight, readWidth]);
                var span = tensor.Buffer.Span;
                int plane = readWidth * readHeight;
                for (int y = 0; y < readHeight; y++)
                {
                    // Past the image, its last row and column repeated.
                    int sy = Math.Min(ry0 + y, ry1 - 1);
                    for (int x = 0; x < readWidth; x++)
                    {
                        int sx = Math.Min(rx0 + x, rx1 - 1);
                        long at = ((long)sy * width + sx) * 4;
                        int to = y * readWidth + x;

                        // GDI+ stores blue, green, red, alpha.
                        span[to] = pixels[at + 2] / 255f;
                        span[plane + to] = pixels[at + 1] / 255f;
                        span[2 * plane + to] = pixels[at] / 255f;
                    }
                }

                using (var results = session.Run([NamedOnnxValue.CreateFromTensor(input, tensor)], [name]))
                {
                    var result = results.First().AsTensor<float>();
                    int resultWidth = result.Dimensions[3];
                    int resultPlane = result.Dimensions[2] * resultWidth;
                    var values = result is DenseTensor<float> dense ? dense.Buffer.Span : result.ToArray();
                    for (int oy = y0 * factor; oy < y1 * factor; oy++)
                    {
                        int ty = oy - ry0 * factor;
                        for (int ox = x0 * factor; ox < x1 * factor; ox++)
                        {
                            int from = ty * resultWidth + (ox - rx0 * factor);
                            long to = ((long)oy * outWidth + ox) * 4;
                            output[to] = Byte(values[2 * resultPlane + from]);
                            output[to + 1] = Byte(values[resultPlane + from]);
                            output[to + 2] = Byte(values[from]);
                            output[to + 3] = 255;
                        }
                    }
                }

                progress?.Invoke(++done / (double)(rows * columns));
            }
        }

        EnlargeAlpha(pixels, width, height, output, factor);
        return ToBitmap(output, outWidth, outHeight);
    }

    private static InferenceSession Load(string model)
    {
        OcrEngineFiles.ResolveNativeLibraries();

        // Without the arena, what a model takes while it runs is given back once it ends. Half the cores: an
        // upscale takes seconds a tile, the UI and the other passes keep the rest.
        using var options = new SessionOptions
        {
            LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_ERROR,
            IntraOpNumThreads = Math.Clamp(Environment.ProcessorCount / 2, 2, 8),
            EnableCpuMemArena = false,
            EnableMemoryPattern = false,
        };
        return new InferenceSession(UpscaleEngineFiles.PathOf(model), options);
    }

    private static int Padded(int side) => (side + SideMultiple - 1) / SideMultiple * SideMultiple;

    private static byte Byte(float value) => (byte)Math.Clamp((int)MathF.Round(value * 255), 0, 255);

    /// <summary>The image's alpha, enlarged bilinear into <paramref name="output"/>; nothing when it is opaque all over.</summary>
    private static void EnlargeAlpha(byte[] pixels, int width, int height, byte[] output, int factor)
    {
        bool opaque = true;
        for (long i = 3; i < pixels.LongLength && opaque; i += 4)
        {
            opaque = pixels[i] == 255;
        }

        if (opaque)
        {
            return;
        }

        int outWidth = width * factor, outHeight = height * factor;
        for (int oy = 0; oy < outHeight; oy++)
        {
            float fy = Math.Clamp((oy + 0.5f) / factor - 0.5f, 0, height - 1);
            int y0 = (int)fy, y1 = Math.Min(y0 + 1, height - 1);
            float wy = fy - y0;
            for (int ox = 0; ox < outWidth; ox++)
            {
                float fx = Math.Clamp((ox + 0.5f) / factor - 0.5f, 0, width - 1);
                int x0 = (int)fx, x1 = Math.Min(x0 + 1, width - 1);
                float wx = fx - x0;
                float top = (pixels[((long)y0 * width + x0) * 4 + 3] * (1 - wx)) + (pixels[((long)y0 * width + x1) * 4 + 3] * wx);
                float bottom = (pixels[((long)y1 * width + x0) * 4 + 3] * (1 - wx)) + (pixels[((long)y1 * width + x1) * 4 + 3] * wx);
                output[((long)oy * outWidth + ox) * 4 + 3] = (byte)Math.Round((top * (1 - wy)) + (bottom * wy));
            }
        }
    }

    // The bitmap's pixels as 32bpp ARGB, row after row without padding.
    private static byte[] ReadPixels(Bitmap source)
    {
        int width = source.Width, height = source.Height;
        var pixels = new byte[(long)width * height * 4];
        var data = source.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * width * 4, width * 4);
            }
        }
        finally
        {
            source.UnlockBits(data);
        }

        return pixels;
    }

    private static Bitmap ToBitmap(byte[] pixels, int width, int height)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(pixels, y * width * 4, data.Scan0 + y * data.Stride, width * 4);
            }
        }
        catch
        {
            bitmap.UnlockBits(data);
            bitmap.Dispose();
            throw;
        }

        bitmap.UnlockBits(data);
        return bitmap;
    }
}

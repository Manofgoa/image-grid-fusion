using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ImageGridFusion.Imaging;

/// <summary>
/// How a text is drawn into a bitmap by <see cref="TextRaster"/>: its font, weight and color, an outline
/// around its letters, a box behind it. The outline's width and the box's margin are fractions of a line.
/// </summary>
public sealed record LayerTextStyle(string Font, bool Bold, Color Color, bool Outline, Color OutlineColor, double OutlineWidth, bool Background, Color BackgroundColor, double BackgroundOpacity)
{
    public const double MinOutlineWidth = 0.02;
    public const double MaxOutlineWidth = 0.2;

    /// <summary>The box's margin around the text, in lines.</summary>
    public const double BackgroundMargin = 0.15;

    /// <summary>Segoe UI bold, white, outlined black over 8 % of a line, no box (workfiles/20261029-layers.md).</summary>
    public static readonly LayerTextStyle Default = new("Segoe UI", true, Color.White, true, Color.Black, 0.08, false, Color.Black, 0.5);
}

/// <summary>
/// Draws a text into a bitmap with DirectWrite through Direct2D — the emojis in their own colors, which
/// GDI and GDI+ cannot draw (workfiles/20261029-layers.md § Rendering). Windows' own components, called
/// through their COM tables here rather than through a library; used on the UI thread.
/// </summary>
internal static unsafe class TextRaster
{
    /// <summary>The font size the text is drawn at, in pixels: large enough for a 4K export, drawn once per change.</summary>
    public const float EmSize = 192;

    // The longer side of the bitmap: a longer text is drawn smaller.
    private const int MaxSide = 8192;

    // Rings of copies drawn around the text, in its outline's color, under it.
    private static readonly int[] OutlineRings = [40, 24, 12];

    private static readonly Guid FactoryId = new("06152247-6f50-465a-9245-118bfd3b6007");
    private static readonly Guid WriteFactoryId = new("b859ee5a-d838-4b5b-a2e8-1adc7d93db48");
    private static readonly Guid ImagingFactoryClass = new("cacaf262-9370-4615-a13b-9f5539da4c0a");
    private static readonly Guid ImagingFactoryId = new("ec5ec8a9-c395-4314-9c77-54d7a935ff70");
    private static readonly Guid PremultipliedBgra = new("6fddc324-4e03-4bfe-b185-3d77768dc910");

    /// <summary>
    /// The text drawn in <paramref name="style"/>, on a transparent background, and the height of one of its
    /// lines in the bitmap's pixels — what a text layer's size measures. An empty text gives an empty box
    /// half a line wide.
    /// </summary>
    public static (Bitmap Bitmap, double LinePixels) Render(string text, LayerTextStyle style)
    {
        var factories = Factories.Get();
        float em = EmSize;
        for (int attempt = 0; ; attempt++)
        {
            var layout = CreateLayout(factories.Write, text.Length == 0 ? " " : text, style, em);
            try
            {
                var metrics = Metrics(layout);
                double line = metrics.Height / Math.Max(1, metrics.LineCount);
                float width = text.Length == 0 ? (float)line / 2 : metrics.WidthIncludingTrailingWhitespace;
                double pad = Math.Max(style.Outline ? style.OutlineWidth * line : 0, style.Background ? LayerTextStyle.BackgroundMargin * line : 0) + 2;
                int w = (int)Math.Ceiling(width + 2 * pad), h = (int)Math.Ceiling(metrics.Height + 2 * pad);
                if (Math.Max(w, h) > MaxSide && attempt == 0)
                {
                    em *= MaxSide / (float)Math.Max(w, h) * 0.98f;
                    continue;
                }

                var bitmap = Draw(factories, layout, text.Length == 0, style, new Size(Math.Max(1, w), Math.Max(1, h)), (float)pad, width, metrics.Height, line);
                return (bitmap, line);
            }
            finally
            {
                Marshal.Release(layout);
            }
        }
    }

    /// <summary>The families of the fonts installed, A→Z, for the font drop-down.</summary>
    public static IReadOnlyList<string> FontFamilies()
    {
        using var fonts = new System.Drawing.Text.InstalledFontCollection();
        return [.. fonts.Families.Select(f => f.Name).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)];
    }

    private static IntPtr CreateLayout(IntPtr write, string text, LayerTextStyle style, float em)
    {
        IntPtr format;
        fixed (char* family = style.Font, locale = "en-us")
        {
            // CreateTextFormat: the family, the system's collection, the weight, normal style and stretch, the size, the locale.
            Check(((delegate* unmanaged[Stdcall]<IntPtr, char*, IntPtr, int, int, int, float, char*, IntPtr*, int>)Slot(write, 15))(
                write, family, IntPtr.Zero, style.Bold ? 700 : 400, 0, 5, em, locale, &format));
        }

        try
        {
            IntPtr layout;
            fixed (char* chars = text)
            {
                // CreateTextLayout, wide enough that only the line breaks of the text end a line.
                Check(((delegate* unmanaged[Stdcall]<IntPtr, char*, int, IntPtr, float, float, IntPtr*, int>)Slot(write, 18))(
                    write, chars, text.Length, format, 1e6f, 1e6f, &layout));
            }

            // SetWordWrapping(NO_WRAP), then the lines centered within the widest once it is known.
            Check(((delegate* unmanaged[Stdcall]<IntPtr, int, int>)Slot(layout, 5))(layout, 1));
            var metrics = Metrics(layout);
            Check(((delegate* unmanaged[Stdcall]<IntPtr, float, int>)Slot(layout, 28))(layout, Math.Max(1, metrics.WidthIncludingTrailingWhitespace)));
            Check(((delegate* unmanaged[Stdcall]<IntPtr, int, int>)Slot(layout, 3))(layout, 2));
            return layout;
        }
        finally
        {
            Marshal.Release(format);
        }
    }

    private static TextMetrics Metrics(IntPtr layout)
    {
        TextMetrics metrics;
        Check(((delegate* unmanaged[Stdcall]<IntPtr, TextMetrics*, int>)Slot(layout, 60))(layout, &metrics));
        return metrics;
    }

    private static Bitmap Draw(Factories factories, IntPtr layout, bool empty, LayerTextStyle style, Size size, float pad, float width, float height, double line)
    {
        IntPtr wic = IntPtr.Zero, target = IntPtr.Zero;
        try
        {
            var format = PremultipliedBgra;

            // IWICImagingFactory.CreateBitmap, cached on load.
            Check(((delegate* unmanaged[Stdcall]<IntPtr, int, int, Guid*, int, IntPtr*, int>)Slot(factories.Imaging, 17))(
                factories.Imaging, size.Width, size.Height, &format, 2, &wic));

            // ID2D1Factory.CreateWicBitmapRenderTarget: BGRA premultiplied, at 96 dpi.
            var properties = new RenderTargetProperties { Format = 87, AlphaMode = 1, DpiX = 96, DpiY = 96 };
            Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, RenderTargetProperties*, IntPtr*, int>)Slot(factories.Direct2D, 13))(
                factories.Direct2D, wic, &properties, &target));

            ((delegate* unmanaged[Stdcall]<IntPtr, void>)Slot(target, 48))(target);
            var clear = default(D2DColor);
            ((delegate* unmanaged[Stdcall]<IntPtr, D2DColor*, void>)Slot(target, 47))(target, &clear);

            // Grayscale antialiasing: ClearType needs an opaque background.
            ((delegate* unmanaged[Stdcall]<IntPtr, int, void>)Slot(target, 34))(target, 2);

            if (style.Background)
            {
                float margin = (float)(LayerTextStyle.BackgroundMargin * line);
                var box = new D2DRect { Left = pad - margin, Top = pad - margin, Right = pad + width + margin, Bottom = pad + height + margin };
                Fill(target, box, Color.FromArgb((int)Math.Round(Math.Clamp(style.BackgroundOpacity, 0, 1) * 255), style.BackgroundColor));
            }

            if (!empty)
            {
                // The outline: copies around the text in its color, its emojis as plain shapes of that color.
                if (style.Outline)
                {
                    double radius = style.OutlineWidth * line;
                    IntPtr outline = Brush(target, style.OutlineColor);
                    try
                    {
                        for (int ring = 0; ring < OutlineRings.Length; ring++)
                        {
                            double r = radius * (OutlineRings.Length - ring) / OutlineRings.Length;
                            int copies = OutlineRings[ring];
                            for (int k = 0; k < copies; k++)
                            {
                                double angle = 2 * Math.PI * k / copies;
                                DrawText(target, layout, outline, pad + (float)(r * Math.Cos(angle)), pad + (float)(r * Math.Sin(angle)), colorFont: false);
                            }
                        }
                    }
                    finally
                    {
                        Marshal.Release(outline);
                    }
                }

                IntPtr brush = Brush(target, style.Color);
                try
                {
                    DrawText(target, layout, brush, pad, pad, colorFont: true);
                }
                finally
                {
                    Marshal.Release(brush);
                }
            }

            Check(((delegate* unmanaged[Stdcall]<IntPtr, ulong*, ulong*, int>)Slot(target, 49))(target, null, null));

            var bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
            var data = bitmap.LockBits(new Rectangle(Point.Empty, size), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try
            {
                // IWICBitmapSource.CopyPixels: the whole bitmap, row by row at the GDI+ bitmap's stride.
                Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int, int, IntPtr, int>)Slot(wic, 7))(
                    wic, IntPtr.Zero, data.Stride, data.Stride * size.Height, data.Scan0));
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            return bitmap;
        }
        finally
        {
            if (target != IntPtr.Zero)
            {
                Marshal.Release(target);
            }

            if (wic != IntPtr.Zero)
            {
                Marshal.Release(wic);
            }
        }
    }

    /// <summary>ID2D1RenderTarget.DrawTextLayout, unsnapped so the outline's copies land between pixels; the color glyphs on demand.</summary>
    private static void DrawText(IntPtr target, IntPtr layout, IntPtr brush, float x, float y, bool colorFont) =>
        ((delegate* unmanaged[Stdcall]<IntPtr, D2DPoint, IntPtr, IntPtr, int, void>)Slot(target, 28))(
            target, new D2DPoint { X = x, Y = y }, layout, brush, colorFont ? 0x5 : 0x1);

    private static void Fill(IntPtr target, D2DRect rect, Color color)
    {
        IntPtr brush = Brush(target, color);
        try
        {
            ((delegate* unmanaged[Stdcall]<IntPtr, D2DRect*, IntPtr, void>)Slot(target, 17))(target, &rect, brush);
        }
        finally
        {
            Marshal.Release(brush);
        }
    }

    /// <summary>ID2D1RenderTarget.CreateSolidColorBrush: the caller's to release.</summary>
    private static IntPtr Brush(IntPtr target, Color color)
    {
        var d2d = new D2DColor { R = color.R / 255f, G = color.G / 255f, B = color.B / 255f, A = color.A / 255f };
        IntPtr brush;
        Check(((delegate* unmanaged[Stdcall]<IntPtr, D2DColor*, IntPtr, IntPtr*, int>)Slot(target, 8))(target, &d2d, IntPtr.Zero, &brush));
        return brush;
    }

    private static void* Slot(IntPtr com, int index) => (*(void***)com)[index];

    private static void Check(int result)
    {
        if (result < 0)
        {
            Marshal.ThrowExceptionForHR(result);
        }
    }

    [DllImport("d2d1.dll", ExactSpelling = true)]
    private static extern int D2D1CreateFactory(int type, in Guid id, IntPtr options, out IntPtr factory);

    [DllImport("dwrite.dll", ExactSpelling = true)]
    private static extern int DWriteCreateFactory(int type, in Guid id, out IntPtr factory);

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoCreateInstance(in Guid classId, IntPtr outer, int context, in Guid id, out IntPtr instance);

    /// <summary>The three factories, made once per thread: the Direct2D one is single-threaded.</summary>
    private sealed class Factories
    {
        [ThreadStatic]
        private static Factories? _current;

        private Factories()
        {
            Check(D2D1CreateFactory(0, FactoryId, IntPtr.Zero, out var direct2D));
            Check(DWriteCreateFactory(0, WriteFactoryId, out var write));
            Check(CoCreateInstance(ImagingFactoryClass, IntPtr.Zero, 1, ImagingFactoryId, out var imaging));
            this.Direct2D = direct2D;
            this.Write = write;
            this.Imaging = imaging;
        }

        public IntPtr Direct2D { get; }

        public IntPtr Write { get; }

        public IntPtr Imaging { get; }

        public static Factories Get() => _current ??= new Factories();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct D2DColor
    {
        public float R, G, B, A;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct D2DPoint
    {
        public float X, Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct D2DRect
    {
        public float Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RenderTargetProperties
    {
        public int Type, Format, AlphaMode;
        public float DpiX, DpiY;
        public int Usage, MinLevel;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TextMetrics
    {
        public float Left, Top, Width, WidthIncludingTrailingWhitespace, Height, LayoutWidth, LayoutHeight;
        public uint MaxBidiReorderingDepth, LineCount;
    }
}

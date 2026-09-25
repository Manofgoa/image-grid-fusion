using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ImageGridFusion.Imaging;

/// <summary>
/// Writes an animated GIF that loops forever with Windows' own WIC GIF encoder, declared here rather
/// than through a third-party library: each frame reduced to a 256-color palette of its own, shown
/// for its duration in hundredths of a second. No sound: GIF carries none. Used from a single
/// thread-pool thread.
/// </summary>
internal sealed class GifEncoder : IFrameEncoder
{
    private const uint GenericWrite = 0x40000000;
    private const int NoCache = 2;
    private const int ErrorDiffusion = 8;
    private const int CustomPalette = 0;
    private const int PaletteColors = 256;

    private static readonly Guid ImagingFactoryClass = new("cacaf262-9370-4615-a13b-9f5539da4c0a");
    private static readonly Guid GifContainer = new("1f8a5601-7d4d-4cbd-9c82-1bc8d4eeb9a5");
    private static readonly Guid Bgr32 = new("6fddc324-4e03-4bfe-b185-3d77768dc90e");
    private static readonly Guid Indexed8 = new("6fddc324-4e03-4bfe-b185-3d77768dc904");

    // The NETSCAPE2.0 extension's sub-block: its size, the loop sub-block id, a loop count of 0 (forever), the terminator.
    private static readonly byte[] LoopForever = [3, 1, 0, 0, 0];

    private readonly IWICImagingFactory _factory;
    private readonly IWICStream _stream;
    private readonly IWICBitmapEncoder _encoder;
    private readonly Size _size;

    /// <summary>Where the written frames end, in hundredths of a second: each delay is cut from the rounded timeline, so the total stays exact.</summary>
    private long _writtenUntil;

    private GifEncoder(IWICImagingFactory factory, IWICStream stream, IWICBitmapEncoder encoder, Size size)
    {
        _factory = factory;
        _stream = stream;
        _encoder = encoder;
        _size = size;
    }

    /// <summary>Creates the file, set to loop forever.</summary>
    public static GifEncoder Create(string path, Size size)
    {
        IWICImagingFactory? factory = null;
        IWICStream? stream = null;
        IWICBitmapEncoder? encoder = null;
        IWICMetadataQueryWriter? metadata = null;
        try
        {
            factory = (IWICImagingFactory)Activator.CreateInstance(Type.GetTypeFromCLSID(ImagingFactoryClass, throwOnError: true)!)!;
            factory.CreateStream(out stream);
            stream.InitializeFromFilename(path, GenericWrite);
            factory.CreateEncoder(GifContainer, IntPtr.Zero, out encoder);
            encoder.Initialize(stream, NoCache);
            encoder.GetMetadataQueryWriter(out metadata);
            SetBytes(metadata, "/appext/Application", "NETSCAPE2.0"u8.ToArray());
            SetBytes(metadata, "/appext/Data", LoopForever);
            return new GifEncoder(factory, stream, encoder, size);
        }
        catch
        {
            MediaFoundation.Release(encoder);
            MediaFoundation.Release(stream);
            MediaFoundation.Release(factory);
            throw;
        }
        finally
        {
            MediaFoundation.Release(metadata);
        }
    }

    public void WriteFrame(Bitmap frame, TimeSpan time, TimeSpan duration)
    {
        long until = (long)Math.Round((time + duration).TotalSeconds * 100);
        ushort delay = (ushort)Math.Clamp(until - _writtenUntil, 1, ushort.MaxValue);
        _writtenUntil = until;

        IWICBitmapSource? source = null;
        IWICPalette? palette = null;
        IWICFormatConverter? indexed = null;
        IWICBitmapFrameEncode? encoded = null;
        IWICMetadataQueryWriter? metadata = null;
        BitmapData? data = frame.LockBits(new Rectangle(Point.Empty, _size), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        try
        {
            // The factory copies the pixels: the bitmap is unlocked as soon as the copy exists.
            _factory.CreateBitmapFromMemory(_size.Width, _size.Height, Bgr32, data.Stride, data.Stride * _size.Height, data.Scan0, out source);
            frame.UnlockBits(data);
            data = null;

            _factory.CreatePalette(out palette);
            palette.InitializeFromBitmap(source, PaletteColors, addTransparentColor: false);
            _factory.CreateFormatConverter(out indexed);
            indexed.Initialize(source, Indexed8, ErrorDiffusion, palette, 0, CustomPalette);

            _encoder.CreateNewFrame(out encoded, IntPtr.Zero);
            encoded.Initialize(IntPtr.Zero);
            encoded.SetSize(_size.Width, _size.Height);
            var format = Indexed8;
            encoded.SetPixelFormat(ref format);
            encoded.SetPalette(palette);

            // Set before the pixels: written after them, the frame goes out without its Graphic Control Extension.
            encoded.GetMetadataQueryWriter(out metadata);
            var value = new WicPropVariant { Type = WicPropVariant.VtUi2, UInt16 = delay };
            metadata.SetMetadataByName("/grctlext/Delay", ref value);
            encoded.WriteSource((IWICBitmapSource)indexed, IntPtr.Zero);
            encoded.Commit();
        }
        finally
        {
            if (data is not null)
            {
                frame.UnlockBits(data);
            }

            MediaFoundation.Release(metadata);
            MediaFoundation.Release(encoded);
            MediaFoundation.Release(indexed);
            MediaFoundation.Release(palette);
            MediaFoundation.Release(source);
        }
    }

    public void Finish() => _encoder.Commit();

    /// <summary>Releases the file: without <see cref="Finish"/>, it is left incomplete and the caller deletes it.</summary>
    public void Dispose()
    {
        MediaFoundation.Release(_encoder);
        MediaFoundation.Release(_stream);
        MediaFoundation.Release(_factory);
    }

    private static void SetBytes(IWICMetadataQueryWriter metadata, string name, byte[] bytes)
    {
        var buffer = Marshal.AllocCoTaskMem(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, buffer, bytes.Length);
            var value = new WicPropVariant { Type = WicPropVariant.VtVectorUi1, Count = (uint)bytes.Length, Items = buffer };
            metadata.SetMetadataByName(name, ref value);
        }
        finally
        {
            Marshal.FreeCoTaskMem(buffer);
        }
    }

    /// <summary>A PROPVARIANT holding an unsigned 16-bit integer or a byte vector, laid out for 64-bit processes (the app is win-x64 only).</summary>
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct WicPropVariant
    {
        public const ushort VtUi2 = 18;
        public const ushort VtVectorUi1 = 0x1000 | 17;

        [FieldOffset(0)]
        public ushort Type;

        [FieldOffset(8)]
        public ushort UInt16;

        [FieldOffset(8)]
        public uint Count;

        [FieldOffset(16)]
        public IntPtr Items;
    }

    // WIC interfaces: only the calls the encoder makes are typed; the other slots keep the vtable order.

    [ComImport, Guid("ec5ec8a9-c395-4314-9c77-54d7a935ff70"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IWICImagingFactory
    {
        void CreateDecoderFromFilename();
        void CreateDecoderFromStream();
        void CreateDecoderFromFileHandle();
        void CreateComponentInfo();
        void CreateDecoder();
        void CreateEncoder(in Guid containerFormat, IntPtr vendor, out IWICBitmapEncoder encoder);
        void CreatePalette(out IWICPalette palette);
        void CreateFormatConverter(out IWICFormatConverter converter);
        void CreateBitmapScaler();
        void CreateBitmapClipper();
        void CreateBitmapFlipRotator();
        void CreateStream(out IWICStream stream);
        void CreateColorContext();
        void CreateColorTransformer();
        void CreateBitmap();
        void CreateBitmapFromSource();
        void CreateBitmapFromSourceRect();
        void CreateBitmapFromMemory(int width, int height, in Guid pixelFormat, int stride, int bufferSize, IntPtr buffer, out IWICBitmapSource bitmap);
    }

    [ComImport, Guid("135ff860-22b7-4ddf-b0f6-218f4f299a43"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IWICStream
    {
        // ISequentialStream, IStream
        void Read();
        void Write();
        void Seek();
        void SetSize();
        void CopyTo();
        void Commit();
        void Revert();
        void LockRegion();
        void UnlockRegion();
        void Stat();
        void Clone();

        void InitializeFromIStream();
        void InitializeFromFilename([MarshalAs(UnmanagedType.LPWStr)] string fileName, uint desiredAccess);
    }

    [ComImport, Guid("00000103-a8f2-4877-ba0a-fd2b6645fb94"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IWICBitmapEncoder
    {
        void Initialize(IWICStream stream, int cacheOption);
        void GetContainerFormat();
        void GetEncoderInfo();
        void SetColorContexts();
        void SetPalette();
        void SetThumbnail();
        void SetPreview();
        void CreateNewFrame(out IWICBitmapFrameEncode frame, IntPtr options);
        void Commit();
        void GetMetadataQueryWriter(out IWICMetadataQueryWriter writer);
    }

    [ComImport, Guid("00000105-a8f2-4877-ba0a-fd2b6645fb94"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IWICBitmapFrameEncode
    {
        void Initialize(IntPtr options);
        void SetSize(int width, int height);
        void SetResolution();
        void SetPixelFormat(ref Guid pixelFormat);
        void SetColorContexts();
        void SetPalette(IWICPalette palette);
        void SetThumbnail();
        void WritePixels();
        void WriteSource(IWICBitmapSource source, IntPtr rectangle);
        void Commit();
        void GetMetadataQueryWriter(out IWICMetadataQueryWriter writer);
    }

    [ComImport, Guid("00000120-a8f2-4877-ba0a-fd2b6645fb94"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IWICBitmapSource
    {
    }

    [ComImport, Guid("00000040-a8f2-4877-ba0a-fd2b6645fb94"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IWICPalette
    {
        void InitializePredefined();
        void InitializeCustom();
        void InitializeFromBitmap(IWICBitmapSource source, int colorCount, [MarshalAs(UnmanagedType.Bool)] bool addTransparentColor);
    }

    [ComImport, Guid("00000301-a8f2-4877-ba0a-fd2b6645fb94"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IWICFormatConverter
    {
        // IWICBitmapSource
        void GetSize();
        void GetPixelFormat();
        void GetResolution();
        void CopyPalette();
        void CopyPixels();

        void Initialize(IWICBitmapSource source, in Guid destinationFormat, int dither, IWICPalette palette, double alphaThresholdPercent, int paletteTranslate);
    }

    [ComImport, Guid("a721791a-0def-4d06-bd91-2118bf1db10b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IWICMetadataQueryWriter
    {
        // IWICMetadataQueryReader
        void GetContainerFormat();
        void GetLocation();
        void GetMetadataByName();
        void GetEnumerator();

        void SetMetadataByName([MarshalAs(UnmanagedType.LPWStr)] string name, ref WicPropVariant value);
    }
}

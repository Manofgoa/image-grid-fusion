using System.Runtime.InteropServices;

namespace ImageGridFusion.Imaging;

/// <summary>
/// The few Media Foundation calls the app needs to decode videos frame by frame (Source Reader) and
/// encode MP4 files (Sink Writer), declared here rather than through a third-party library. Objects
/// are free-threaded: they are created and used on thread-pool threads only, never on the UI thread.
/// </summary>
internal static class MediaFoundation
{
    private const int Version = 0x00020070;

    public const int FirstVideoStream = unchecked((int)0xFFFFFFFC);
    public const int FirstAudioStream = unchecked((int)0xFFFFFFFD);
    public const int AllStreams = unchecked((int)0xFFFFFFFE);
    public const int MediaSource = unchecked((int)0xFFFFFFFF);

    public const int EndOfStreamFlag = 0x2;
    public const int CurrentMediaTypeChangedFlag = 0x20;

    private static readonly Lazy<bool> Started = new(() =>
    {
        Marshal.ThrowExceptionForHR(MFStartup(Version, 0));
        return true;
    });

    public static class Keys
    {
        public static readonly Guid EnableVideoProcessing = new("fb394f3d-ccf1-42ee-bbb3-f9b845d5681d");
        public static readonly Guid MajorType = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
        public static readonly Guid Subtype = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
        public static readonly Guid FrameSize = new("1652c33d-d6b2-4012-b834-72030849a37d");
        public static readonly Guid FrameRate = new("c459a2e8-3d2c-4e44-b132-fee5156c7bb0");
        public static readonly Guid PixelAspectRatio = new("c6376a1e-8d0a-4027-be45-6d9a0ad39bb6");
        public static readonly Guid InterlaceMode = new("e2724bb8-e676-4806-b4b2-a8d6efb44ccd");
        public static readonly Guid AverageBitrate = new("20332624-fb0d-4d9e-bd0d-cbf6786c102e");
        public static readonly Guid DefaultStride = new("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");
        public static readonly Guid MinimumDisplayAperture = new("d7388766-18fe-48c6-a177-ee894867c8c4");
        public static readonly Guid VideoRotation = new("c380465d-2271-428c-9b83-ecea3b4a85c1");
        public static readonly Guid Mpeg2Profile = new("ad76a80b-2d5c-4e0b-b375-64e520137036");
        public static readonly Guid AudioChannels = new("37e48bf5-645e-4c5b-89de-ada9e29b696a");
        public static readonly Guid AudioSamplesPerSecond = new("5faeeae7-0290-4c31-9e8a-c534f68d9dba");
        public static readonly Guid AudioBitsPerSample = new("f2deb57f-40fa-4764-aa33-ed4f2d1ff669");
        public static readonly Guid AudioAverageBytesPerSecond = new("1aab75c8-cfef-451c-ab95-ac034b8e1731");
        public static readonly Guid AudioBlockAlignment = new("322de230-9eeb-43bd-ab7a-ff412251541d");
        public static readonly Guid Duration = new("6c990d33-bb8e-477a-8598-0d5d96fcd88a");
    }

    public static class Formats
    {
        public static readonly Guid Video = new("73646976-0000-0010-8000-00aa00389b71");
        public static readonly Guid Audio = new("73647561-0000-0010-8000-00aa00389b71");
        public static readonly Guid Rgb32 = new("00000016-0000-0010-8000-00aa00389b71");
        public static readonly Guid H264 = new("34363248-0000-0010-8000-00aa00389b71");
        public static readonly Guid Pcm = new("00000001-0000-0010-8000-00aa00389b71");
        public static readonly Guid Aac = new("00001610-0000-0010-8000-00aa00389b71");
    }

    public const int ProgressiveInterlace = 2;
    public const int H264HighProfile = 100;

    public static void EnsureStarted() => _ = Started.Value;

    public static IMFAttributes CreateAttributes(int size = 1)
    {
        EnsureStarted();
        Marshal.ThrowExceptionForHR(MFCreateAttributes(out var attributes, size));
        return attributes;
    }

    public static IMFMediaType CreateMediaType()
    {
        EnsureStarted();
        Marshal.ThrowExceptionForHR(MFCreateMediaType(out var type));
        return type;
    }

    public static IMFSourceReader CreateSourceReader(string path, IMFAttributes? attributes)
    {
        EnsureStarted();
        Marshal.ThrowExceptionForHR(MFCreateSourceReaderFromURL(Path.GetFullPath(path), attributes, out var reader));
        return reader;
    }

    public static IMFSinkWriter CreateSinkWriter(string path, IMFAttributes? attributes)
    {
        EnsureStarted();
        Marshal.ThrowExceptionForHR(MFCreateSinkWriterFromURL(Path.GetFullPath(path), IntPtr.Zero, attributes, out var writer));
        return writer;
    }

    public static IMFSample CreateSample()
    {
        Marshal.ThrowExceptionForHR(MFCreateSample(out var sample));
        return sample;
    }

    public static IMFMediaBuffer CreateMemoryBuffer(int length)
    {
        Marshal.ThrowExceptionForHR(MFCreateMemoryBuffer(length, out var buffer));
        return buffer;
    }

    /// <summary>Two 32-bit values packed in one attribute, like a frame size or a frame rate.</summary>
    public static void SetPair(IMFAttributes attributes, Guid key, int high, int low) =>
        attributes.SetUINT64(key, ((long)high << 32) | (uint)low);

    public static (int High, int Low) GetPair(IMFAttributes attributes, Guid key)
    {
        attributes.GetUINT64(key, out long value);
        return ((int)(value >> 32), (int)(value & 0xFFFFFFFF));
    }

    public static int? TryGetInt(IMFAttributes attributes, Guid key) =>
        attributes.GetUINT32(key, out int value) >= 0 ? value : null;

    /// <summary>Releases a COM object now: samples and buffers come from small pools that run dry otherwise.</summary>
    public static void Release(object? comObject)
    {
        if (comObject is not null && Marshal.IsComObject(comObject))
        {
            Marshal.ReleaseComObject(comObject);
        }
    }

    [DllImport("mfplat.dll", ExactSpelling = true)]
    private static extern int MFStartup(int version, int flags);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    private static extern int MFCreateAttributes(out IMFAttributes attributes, int initialSize);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    private static extern int MFCreateMediaType(out IMFMediaType type);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    private static extern int MFCreateSample(out IMFSample sample);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    private static extern int MFCreateMemoryBuffer(int maxLength, out IMFMediaBuffer buffer);

    [DllImport("mfreadwrite.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int MFCreateSourceReaderFromURL(string url, IMFAttributes? attributes, out IMFSourceReader reader);

    [DllImport("mfreadwrite.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int MFCreateSinkWriterFromURL(string url, IntPtr byteStream, IMFAttributes? attributes, out IMFSinkWriter writer);
}

/// <summary>A PROPVARIANT holding a 64-bit integer, the only kind the app passes or reads.</summary>
[StructLayout(LayoutKind.Explicit, Size = 24)]
internal struct PropVariant
{
    private const ushort VtI8 = 20;

    [FieldOffset(0)]
    public ushort Type;

    [FieldOffset(8)]
    public long Value;

    public static PropVariant FromLong(long value) => new() { Type = VtI8, Value = value };
}

[ComImport, Guid("2cd2d921-c447-44a7-a13c-4adabfc247e3"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFAttributes
{
    [PreserveSig] int GetItem(in Guid key, IntPtr value);
    [PreserveSig] int GetItemType(in Guid key, out int type);
    [PreserveSig] int CompareItem(in Guid key, IntPtr value, out bool result);
    [PreserveSig] int Compare(IMFAttributes theirs, int matchType, out bool result);
    [PreserveSig] int GetUINT32(in Guid key, out int value);
    [PreserveSig] int GetUINT64(in Guid key, out long value);
    [PreserveSig] int GetDouble(in Guid key, out double value);
    [PreserveSig] int GetGUID(in Guid key, out Guid value);
    [PreserveSig] int GetStringLength(in Guid key, out int length);
    [PreserveSig] int GetString(in Guid key, IntPtr value, int size, out int length);
    [PreserveSig] int GetAllocatedString(in Guid key, out IntPtr value, out int length);
    [PreserveSig] int GetBlobSize(in Guid key, out int size);
    [PreserveSig] int GetBlob(in Guid key, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] buffer, int size, out int blobSize);
    [PreserveSig] int GetAllocatedBlob(in Guid key, out IntPtr buffer, out int size);
    [PreserveSig] int GetUnknown(in Guid key, in Guid iid, out IntPtr value);
    [PreserveSig] int SetItem(in Guid key, IntPtr value);
    [PreserveSig] int DeleteItem(in Guid key);
    [PreserveSig] int DeleteAllItems();
    void SetUINT32(in Guid key, int value);
    void SetUINT64(in Guid key, long value);
    void SetDouble(in Guid key, double value);
    void SetGUID(in Guid key, in Guid value);
    void SetString(in Guid key, [MarshalAs(UnmanagedType.LPWStr)] string value);
    void SetBlob(in Guid key, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] buffer, int size);
    void SetUnknown(in Guid key, [MarshalAs(UnmanagedType.IUnknown)] object value);
    void LockStore();
    void UnlockStore();
    void GetCount(out int count);
    void GetItemByIndex(int index, out Guid key, IntPtr value);
    void CopyAllItems(IMFAttributes destination);
}

[ComImport, Guid("44ae0fa8-ea31-4109-8d2e-4cae4997c555"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFMediaType : IMFAttributes
{
    [PreserveSig] new int GetItem(in Guid key, IntPtr value);
    [PreserveSig] new int GetItemType(in Guid key, out int type);
    [PreserveSig] new int CompareItem(in Guid key, IntPtr value, out bool result);
    [PreserveSig] new int Compare(IMFAttributes theirs, int matchType, out bool result);
    [PreserveSig] new int GetUINT32(in Guid key, out int value);
    [PreserveSig] new int GetUINT64(in Guid key, out long value);
    [PreserveSig] new int GetDouble(in Guid key, out double value);
    [PreserveSig] new int GetGUID(in Guid key, out Guid value);
    [PreserveSig] new int GetStringLength(in Guid key, out int length);
    [PreserveSig] new int GetString(in Guid key, IntPtr value, int size, out int length);
    [PreserveSig] new int GetAllocatedString(in Guid key, out IntPtr value, out int length);
    [PreserveSig] new int GetBlobSize(in Guid key, out int size);
    [PreserveSig] new int GetBlob(in Guid key, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] buffer, int size, out int blobSize);
    [PreserveSig] new int GetAllocatedBlob(in Guid key, out IntPtr buffer, out int size);
    [PreserveSig] new int GetUnknown(in Guid key, in Guid iid, out IntPtr value);
    [PreserveSig] new int SetItem(in Guid key, IntPtr value);
    [PreserveSig] new int DeleteItem(in Guid key);
    [PreserveSig] new int DeleteAllItems();
    new void SetUINT32(in Guid key, int value);
    new void SetUINT64(in Guid key, long value);
    new void SetDouble(in Guid key, double value);
    new void SetGUID(in Guid key, in Guid value);
    new void SetString(in Guid key, [MarshalAs(UnmanagedType.LPWStr)] string value);
    new void SetBlob(in Guid key, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] buffer, int size);
    new void SetUnknown(in Guid key, [MarshalAs(UnmanagedType.IUnknown)] object value);
    new void LockStore();
    new void UnlockStore();
    new void GetCount(out int count);
    new void GetItemByIndex(int index, out Guid key, IntPtr value);
    new void CopyAllItems(IMFAttributes destination);
    void GetMajorType(out Guid type);
    void IsCompressedFormat(out bool compressed);
    [PreserveSig] int IsEqual(IMFMediaType type, out int flags);
    void GetRepresentation(Guid representation, out IntPtr value);
    void FreeRepresentation(Guid representation, IntPtr value);
}

[ComImport, Guid("c40a00f2-b93a-4d80-ae8c-5a1c634f58e4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFSample : IMFAttributes
{
    [PreserveSig] new int GetItem(in Guid key, IntPtr value);
    [PreserveSig] new int GetItemType(in Guid key, out int type);
    [PreserveSig] new int CompareItem(in Guid key, IntPtr value, out bool result);
    [PreserveSig] new int Compare(IMFAttributes theirs, int matchType, out bool result);
    [PreserveSig] new int GetUINT32(in Guid key, out int value);
    [PreserveSig] new int GetUINT64(in Guid key, out long value);
    [PreserveSig] new int GetDouble(in Guid key, out double value);
    [PreserveSig] new int GetGUID(in Guid key, out Guid value);
    [PreserveSig] new int GetStringLength(in Guid key, out int length);
    [PreserveSig] new int GetString(in Guid key, IntPtr value, int size, out int length);
    [PreserveSig] new int GetAllocatedString(in Guid key, out IntPtr value, out int length);
    [PreserveSig] new int GetBlobSize(in Guid key, out int size);
    [PreserveSig] new int GetBlob(in Guid key, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] buffer, int size, out int blobSize);
    [PreserveSig] new int GetAllocatedBlob(in Guid key, out IntPtr buffer, out int size);
    [PreserveSig] new int GetUnknown(in Guid key, in Guid iid, out IntPtr value);
    [PreserveSig] new int SetItem(in Guid key, IntPtr value);
    [PreserveSig] new int DeleteItem(in Guid key);
    [PreserveSig] new int DeleteAllItems();
    new void SetUINT32(in Guid key, int value);
    new void SetUINT64(in Guid key, long value);
    new void SetDouble(in Guid key, double value);
    new void SetGUID(in Guid key, in Guid value);
    new void SetString(in Guid key, [MarshalAs(UnmanagedType.LPWStr)] string value);
    new void SetBlob(in Guid key, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] buffer, int size);
    new void SetUnknown(in Guid key, [MarshalAs(UnmanagedType.IUnknown)] object value);
    new void LockStore();
    new void UnlockStore();
    new void GetCount(out int count);
    new void GetItemByIndex(int index, out Guid key, IntPtr value);
    new void CopyAllItems(IMFAttributes destination);
    void GetSampleFlags(out int flags);
    void SetSampleFlags(int flags);
    void GetSampleTime(out long time);
    void SetSampleTime(long time);
    [PreserveSig] int GetSampleDuration(out long duration);
    void SetSampleDuration(long duration);
    void GetBufferCount(out int count);
    void GetBufferByIndex(int index, out IMFMediaBuffer buffer);
    void ConvertToContiguousBuffer(out IMFMediaBuffer buffer);
    void AddBuffer(IMFMediaBuffer buffer);
    void RemoveBufferByIndex(int index);
    void RemoveAllBuffers();
    void GetTotalLength(out int length);
    void CopyToBuffer(IMFMediaBuffer buffer);
}

[ComImport, Guid("045fa593-8799-42b8-bc8d-8968c6453507"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFMediaBuffer
{
    void Lock(out IntPtr buffer, out int maxLength, out int currentLength);
    void Unlock();
    void GetCurrentLength(out int length);
    void SetCurrentLength(int length);
    void GetMaxLength(out int length);
}

[ComImport, Guid("7dc9d5f9-9ed9-44ec-9bbf-0600bb589fbb"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMF2DBuffer
{
    void Lock2D(out IntPtr scanline0, out int pitch);
    void Unlock2D();
    void GetScanline0AndPitch(out IntPtr scanline0, out int pitch);
    void IsContiguousFormat(out bool contiguous);
    void GetContiguousLength(out int length);
    void ContiguousCopyTo(IntPtr destination, int length);
    void ContiguousCopyFrom(IntPtr source, int length);
}

[ComImport, Guid("70ae66f2-c809-4e4f-8915-bdcb406b7993"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFSourceReader
{
    void GetStreamSelection(int streamIndex, out bool selected);
    void SetStreamSelection(int streamIndex, bool selected);
    [PreserveSig] int GetNativeMediaType(int streamIndex, int typeIndex, out IMFMediaType type);
    void GetCurrentMediaType(int streamIndex, out IMFMediaType type);
    [PreserveSig] int SetCurrentMediaType(int streamIndex, IntPtr reserved, IMFMediaType type);
    void SetCurrentPosition(in Guid timeFormat, in PropVariant position);
    void ReadSample(int streamIndex, int controlFlags, out int actualStreamIndex, out int streamFlags, out long timestamp, out IMFSample? sample);
    void Flush(int streamIndex);
    void GetServiceForStream(int streamIndex, in Guid service, in Guid iid, out IntPtr value);
    [PreserveSig] int GetPresentationAttribute(int streamIndex, in Guid key, out PropVariant value);
}

[ComImport, Guid("3137f1cd-fe5e-4805-a5d8-fb477448cb3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFSinkWriter
{
    void AddStream(IMFMediaType targetType, out int streamIndex);
    void SetInputMediaType(int streamIndex, IMFMediaType inputType, IMFAttributes? encodingParameters);
    void BeginWriting();
    void WriteSample(int streamIndex, IMFSample sample);
    void SendStreamTick(int streamIndex, long timestamp);
    void PlaceMarker(int streamIndex, IntPtr context);
    void NotifyEndOfSegment(int streamIndex);
    void Flush(int streamIndex);
    void DoFinalize();
    void GetServiceForStream(int streamIndex, in Guid service, in Guid iid, out IntPtr value);
    void GetStatistics(int streamIndex, IntPtr statistics);
}

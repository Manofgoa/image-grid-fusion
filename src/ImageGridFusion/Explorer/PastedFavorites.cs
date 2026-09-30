using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

namespace ImageGridFusion.Explorer;

/// <summary>
/// The folder next to the exe where an image without a file is saved before it becomes a favorite:
/// a pasted image as a PNG, a pasted or dropped text as the text it arrived as. The app makes these
/// files for the favorites, so one of them un-hearted goes to the Recycle Bin.
/// See workfiles/20260930-favorites-drag-drop.md § Images Without a File.
/// </summary>
internal static class PastedFavorites
{
    public const string FolderName = "favorites-from-pasted";

    public static string Folder => Path.Combine(AppContext.BaseDirectory, FolderName);

    /// <summary>Whether <paramref name="fullPath"/> is a file of the folder, not of a subfolder.</summary>
    public static bool Holds(string fullPath) =>
        string.Equals(Path.GetDirectoryName(Path.GetFullPath(fullPath)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Folder)), StringComparison.OrdinalIgnoreCase);

    /// <summary>Saves <paramref name="bitmap"/> as a new PNG of the folder, alpha kept; returns its path.</summary>
    public static string SaveImage(Bitmap bitmap) => Save(".png", stream => bitmap.Save(stream, ImageFormat.Png));

    /// <summary>Saves <paramref name="content"/> in UTF-8 as a new file of the folder, with <paramref name="extension"/>; returns its path.</summary>
    public static string SaveText(string content, string extension) =>
        Save(extension, stream => stream.Write(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content)));

    /// <summary>
    /// Sends the file to the Recycle Bin, without a dialog; a file already gone is left alone.
    /// Throws an <see cref="IOException"/> when the Shell refuses.
    /// </summary>
    public static void Recycle(string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            return;
        }

        var operation = new FileOperation
        {
            Function = Delete,
            From = fullPath + "\0\0",
            Flags = AllowUndo | NoConfirmation | NoErrorUi | Silent,
        };
        int result = SHFileOperation(ref operation);
        if (result != 0 || operation.AnyOperationsAborted)
        {
            throw new IOException($"The file could not be sent to the Recycle Bin (error 0x{result:X}).");
        }
    }

    /// <summary>
    /// Writes a new file named <c>pasted-yyyyMMdd-HHmmss</c> from the local time, <c>-2</c>, <c>-3</c>…
    /// appended while the name is taken; the folder made when missing. Nothing is left behind on failure.
    /// </summary>
    private static string Save(string extension, Action<Stream> write)
    {
        Directory.CreateDirectory(Folder);
        string stem = Path.Combine(Folder, $"pasted-{DateTime.Now:yyyyMMdd-HHmmss}");
        for (int n = 1; ; n++)
        {
            string path = n == 1 ? stem + extension : $"{stem}-{n}{extension}";
            FileStream stream;
            try
            {
                stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
            }
            catch (IOException) when (File.Exists(path))
            {
                continue;
            }

            try
            {
                using (stream)
                {
                    write(stream);
                }
            }
            catch
            {
                File.Delete(path);
                throw;
            }

            return path;
        }
    }

    private const uint Delete = 3;
    private const ushort Silent = 0x0004;
    private const ushort NoConfirmation = 0x0010;
    private const ushort AllowUndo = 0x0040;
    private const ushort NoErrorUi = 0x0400;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FileOperation
    {
        public IntPtr Window;
        public uint Function;
        public string From;
        public string? To;
        public ushort Flags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool AnyOperationsAborted;
        public IntPtr NameMappings;
        public string? ProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref FileOperation operation);
}

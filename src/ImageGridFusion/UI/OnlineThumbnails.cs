using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ImageGridFusion.Web;

namespace ImageGridFusion.UI;

/// <summary>
/// The animated thumbnails of an online library's tiles: each one's small GIF rendition fetched from the
/// library's CDN — a few at a time, never counted against the key's calls — and kept whole in memory, a frame
/// decoded only when shown; the frame shown follows the clock, so every tile in view plays while the results
/// show. The least recently shown dropped past a cap. See workfiles/20261105-online-library-giphy.md § Thumbnails.
/// </summary>
internal sealed class OnlineThumbnails : IDisposable
{
    private const int MaxCached = 200;
    private const int Parallel = 4;

    // A frame's delay below this, in hundredths of a second, is played as browsers do: 1/10 s.
    private const int MinDelay = 2;
    private const int DefaultDelay = 10;

    private static readonly int PropertyFrameDelay = 0x5100;

    private readonly Control _owner;
    private readonly Dictionary<string, LinkedListNode<Entry>> _entries = new(StringComparer.Ordinal);
    private readonly LinkedList<Entry> _order = new();
    private readonly HashSet<string> _pending = new(StringComparer.Ordinal);

    // The thumbnails that could not be had: not asked for again in this list.
    private readonly HashSet<string> _failed = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _slots = new(Parallel);
    private CancellationTokenSource _cancel = new();
    private bool _disposed;

    public OnlineThumbnails(Control owner)
    {
        this._owner = owner;
    }

    /// <summary>A thumbnail arrived — or none could be had — for a key; on the UI thread.</summary>
    public event Action<string>? Loaded;

    /// <summary>Whether a thumbnail plays: one is held.</summary>
    public bool Any => this._entries.Count > 0;

    /// <summary>
    /// The frame <paramref name="key"/>'s thumbnail shows now; null while it is fetched — asked for from
    /// <paramref name="url"/> — or when it could not be had. On the UI thread.
    /// </summary>
    public Image? Frame(string key, Uri url)
    {
        if (this._entries.TryGetValue(key, out var node))
        {
            this._order.Remove(node);
            this._order.AddLast(node);
            return node.Value.Show(Environment.TickCount64);
        }

        this.Request(key, url);
        return null;
    }

    /// <summary>Forgets every thumbnail, those on their way too: a new list.</summary>
    public void Clear()
    {
        this._cancel.Cancel();
        this._cancel.Dispose();
        this._cancel = new CancellationTokenSource();
        this._pending.Clear();
        this._failed.Clear();
        foreach (var entry in this._order)
        {
            entry.Dispose();
        }

        this._order.Clear();
        this._entries.Clear();
    }

    public void Dispose()
    {
        if (this._disposed)
        {
            return;
        }

        this._disposed = true;
        this.Clear();
        this._cancel.Dispose();
        this._slots.Dispose();
    }

    private void Request(string key, Uri url)
    {
        if (this._disposed || this._failed.Contains(key) || !this._pending.Add(key))
        {
            return;
        }

        _ = this.FetchAsync(key, url, this._cancel.Token);
    }

    private async Task FetchAsync(string key, Uri url, CancellationToken token)
    {
        Entry? entry = null;
        try
        {
            await this._slots.WaitAsync(token);
            try
            {
                using var response = await Downloader.GetAsync(url, token);
                if (response.IsSuccessStatusCode)
                {
                    byte[] bytes = await response.Content.ReadAsByteArrayAsync(token);
                    entry = await Task.Run(() => Entry.Decode(key, bytes), token);
                }
            }
            finally
            {
                this._slots.Release();
            }
        }
        catch (Exception e) when (e is UrlLoadException or HttpRequestException or OperationCanceledException or ObjectDisposedException)
        {
            // No thumbnail: the tile stays blank, its title and heart still there.
        }

        if (token.IsCancellationRequested || this._disposed || this._owner.IsDisposed)
        {
            entry?.Dispose();
            return;
        }

        try
        {
            this._owner.BeginInvoke(() => this.Store(key, entry, token));
        }
        catch (InvalidOperationException)
        {
            entry?.Dispose();
        }
    }

    private void Store(string key, Entry? entry, CancellationToken token)
    {
        if (token.IsCancellationRequested || this._disposed || !this._pending.Remove(key))
        {
            entry?.Dispose();
            return;
        }

        if (entry is null)
        {
            this._failed.Add(key);
            this.Loaded?.Invoke(key);
            return;
        }

        this._entries[key] = this._order.AddLast(entry);
        while (this._order.Count > MaxCached)
        {
            var oldest = this._order.First!.Value;
            this._order.RemoveFirst();
            this._entries.Remove(oldest.Key);
            oldest.Dispose();
        }

        this.Loaded?.Invoke(key);
    }

    /// <summary>A GIF held whole — its stream kept, as GDI+ needs it — with when each frame ends, and the frame decoded last.</summary>
    private sealed class Entry : IDisposable
    {
        private readonly MemoryStream _stream;
        private readonly Image _image;
        private readonly int[] _ends;
        private int _shown;

        private Entry(string key, MemoryStream stream, Image image, int[] ends)
        {
            this.Key = key;
            this._stream = stream;
            this._image = image;
            this._ends = ends;
        }

        public string Key { get; }

        /// <summary>The GIF in <paramref name="bytes"/>, its frames' timings read; null when it is no image GDI+ reads.</summary>
        public static Entry? Decode(string key, byte[] bytes)
        {
            var stream = new MemoryStream(bytes, writable: false);
            try
            {
                var image = Image.FromStream(stream);
                int frames = image.FrameDimensionsList.Contains(FrameDimension.Time.Guid) ? image.GetFrameCount(FrameDimension.Time) : 1;
                return new Entry(key, stream, image, Ends(image, frames));
            }
            catch (Exception e) when (e is ArgumentException or ExternalException or OutOfMemoryException)
            {
                stream.Dispose();
                return null;
            }
        }

        /// <summary>The frame shown at <paramref name="now"/>, the GIF looping.</summary>
        public Image Show(long now)
        {
            if (this._ends.Length > 1)
            {
                int at = (int)(now % this._ends[^1]);
                int frame = Array.BinarySearch(this._ends, at + 1);
                frame = frame < 0 ? ~frame : frame;
                frame = Math.Min(frame, this._ends.Length - 1);
                if (frame != this._shown)
                {
                    try
                    {
                        this._image.SelectActiveFrame(FrameDimension.Time, frame);
                        this._shown = frame;
                    }
                    catch (ExternalException)
                    {
                        // A frame GDI+ cannot decode: the one before stays.
                    }
                }
            }

            return this._image;
        }

        public void Dispose()
        {
            this._image.Dispose();
            this._stream.Dispose();
        }

        /// <summary>When each frame ends in ms from the first's start; one frame lasting forever for a still.</summary>
        private static int[] Ends(Image image, int frames)
        {
            var ends = new int[Math.Max(1, frames)];
            byte[]? delays = null;
            try
            {
                delays = image.GetPropertyItem(PropertyFrameDelay)?.Value;
            }
            catch (ArgumentException)
            {
                // No timings: every frame at the default delay.
            }

            int total = 0;
            for (int i = 0; i < ends.Length; i++)
            {
                int delay = delays is not null && delays.Length >= (i + 1) * 4 ? BitConverter.ToInt32(delays, i * 4) : DefaultDelay;
                total += (delay < MinDelay ? DefaultDelay : delay) * 10;
                ends[i] = total;
            }

            return ends;
        }
    }
}

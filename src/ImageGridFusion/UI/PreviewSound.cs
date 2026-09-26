using ImageGridFusion.Composition;
using Windows.Media.Audio;
using Windows.Media.Render;
using Windows.Storage;

namespace ImageGridFusion.UI;

/// <summary>
/// Mixes the sounds of the grid's videos with a Windows audio graph: one node per video with sound,
/// at the gain of its volume effect (up to 200 %), each kept in step with the frames its video shows;
/// and one for the soundtrack, at its level, kept in step with the grid's clock. Used from the UI
/// thread only.
/// </summary>
internal sealed class PreviewSound : IDisposable
{
    /// <summary>Beyond this gap between a sound and its frames, the sound jumps back in step.</summary>
    private static readonly TimeSpan Drift = TimeSpan.FromMilliseconds(250);

    private readonly Dictionary<SourceImage, Voice> _voices = [];

    // The soundtrack's node and its file; null while no soundtrack plays.
    private Voice? _soundtrack;
    private string? _soundtrackPath;

    // Created with the first video with sound, kept until disposed; null when Windows gives none.
    private Task<AudioGraph?>? _graph;
    private AudioDeviceOutputNode? _output;
    private bool _running;
    private bool _disposed;

    /// <summary>Mixes the sounds of the videos with sound among <paramref name="images"/>; the others stop.</summary>
    public void Follow(IEnumerable<SourceImage> images)
    {
        var mixed = images.Where(i => i.HasSound).ToHashSet();
        foreach (var (image, voice) in _voices.ToList())
        {
            if (!mixed.Contains(image))
            {
                voice.Dispose();
                _voices.Remove(image);
            }
        }

        foreach (var image in mixed)
        {
            if (!_voices.ContainsKey(image))
            {
                var voice = new Voice();
                _voices[image] = voice;
                OpenAsync(image.FilePath!, voice);
            }
        }

        UpdateGraph();
    }

    /// <summary>Mixes the sound track of the file at <paramref name="path"/> as the soundtrack; <c>null</c> stops it.</summary>
    public void FollowSoundtrack(string? path)
    {
        if (path == _soundtrackPath)
        {
            return;
        }

        _soundtrack?.Dispose();
        _soundtrack = null;
        _soundtrackPath = path;
        if (path is not null)
        {
            _soundtrack = new Voice();
            OpenAsync(path, _soundtrack);
        }

        UpdateGraph();
    }

    /// <summary>
    /// Plays the sound of <paramref name="image"/> from <paramref name="position"/>, at the gain of its
    /// volume effect, while <paramref name="playing"/> and not muted; else pauses it.
    /// </summary>
    public void Sync(SourceImage image, TimeSpan position, bool playing)
    {
        if (_voices.TryGetValue(image, out var voice))
        {
            Sync(voice, position, image.Look.SoundGain, playing);
        }
    }

    /// <summary>Plays the soundtrack from <paramref name="position"/> at <paramref name="gain"/>.</summary>
    public void SyncSoundtrack(TimeSpan position, double gain)
    {
        if (_soundtrack is { } voice)
        {
            Sync(voice, position, gain, playing: true);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        foreach (var voice in _voices.Values)
        {
            voice.Dispose();
        }

        _voices.Clear();
        _soundtrack?.Dispose();
        _soundtrack = null;
        if (_graph is { IsCompletedSuccessfully: true, Result: { } graph })
        {
            graph.Dispose();
        }
    }

    private static void Sync(Voice voice, TimeSpan position, double gain, bool playing)
    {
        if (voice.Node is not { } node)
        {
            return;
        }

        node.OutgoingGain = gain;
        if (!playing || gain <= 0)
        {
            if (voice.Playing)
            {
                node.Stop();
                voice.Playing = false;
            }

            return;
        }

        if (!voice.Playing || (node.Position - position).Duration() > Drift)
        {
            node.Seek(position < node.Duration ? position : TimeSpan.Zero);
        }

        if (!voice.Playing)
        {
            node.Start();
            voice.Playing = true;
        }
    }

    private async void OpenAsync(string path, Voice voice)
    {
        try
        {
            var graph = await (_graph ??= CreateGraphAsync());
            if (graph is null || _output is null || voice.IsDisposed)
            {
                return;
            }

            var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(path));
            if (voice.IsDisposed)
            {
                return;
            }

            var result = await graph.CreateFileInputNodeAsync(file);
            if (result.Status != AudioFileNodeCreationStatus.Success)
            {
                return;
            }

            var node = result.FileInputNode;
            if (voice.IsDisposed)
            {
                node.Dispose();
                return;
            }

            // Silent until the frames play; looping with its video, or on its own length.
            node.Stop();
            node.LoopCount = null;
            node.AddOutgoingConnection(_output);
            voice.Node = node;
            UpdateGraph();
        }
        catch (Exception)
        {
            // No sound for this video, or no soundtrack, in the preview; the frames still play.
        }
    }

    private async Task<AudioGraph?> CreateGraphAsync()
    {
        var created = await AudioGraph.CreateAsync(new AudioGraphSettings(AudioRenderCategory.Media));
        if (created.Status != AudioGraphCreationStatus.Success)
        {
            return null;
        }

        var graph = created.Graph;
        var output = await graph.CreateDeviceOutputNodeAsync();
        if (output.Status != AudioDeviceNodeCreationStatus.Success || _disposed)
        {
            graph.Dispose();
            return null;
        }

        _output = output.DeviceOutputNode;
        return graph;
    }

    /// <summary>The graph runs while it has a video or a soundtrack to mix, so an idle preview holds no audio stream open.</summary>
    private void UpdateGraph()
    {
        if (_disposed || _graph is not { IsCompletedSuccessfully: true, Result: { } graph })
        {
            return;
        }

        bool needed = _voices.Count > 0 || _soundtrack is not null;
        if (needed && !_running)
        {
            graph.Start();
        }
        else if (!needed && _running)
        {
            graph.Stop();
        }

        _running = needed;
    }

    private sealed class Voice : IDisposable
    {
        public AudioFileInputNode? Node { get; set; }

        public bool Playing { get; set; }

        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
            Node?.Stop();
            Node?.Dispose();
            Node = null;
        }
    }
}

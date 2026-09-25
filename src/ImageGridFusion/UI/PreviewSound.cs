using ImageGridFusion.Composition;
using Windows.Media.Audio;
using Windows.Media.Render;
using Windows.Storage;

namespace ImageGridFusion.UI;

/// <summary>
/// Mixes the sounds of the grid's videos with a Windows audio graph: one node per video with sound,
/// at the gain of its volume effect (up to 200 %), each kept in step with the frames its video shows.
/// Used from the UI thread only.
/// </summary>
internal sealed class PreviewSound : IDisposable
{
    /// <summary>Beyond this gap between a sound and its frames, the sound jumps back in step.</summary>
    private static readonly TimeSpan Drift = TimeSpan.FromMilliseconds(250);

    private readonly Dictionary<SourceImage, Voice> _voices = [];

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
                OpenAsync(image, voice);
            }
        }

        UpdateGraph();
    }

    /// <summary>
    /// Plays the sound of <paramref name="image"/> from <paramref name="position"/>, at the gain of its
    /// volume effect, while <paramref name="playing"/> and not muted; else pauses it.
    /// </summary>
    public void Sync(SourceImage image, TimeSpan position, bool playing)
    {
        if (!_voices.TryGetValue(image, out var voice) || voice.Node is not { } node)
        {
            return;
        }

        double gain = image.Look.SoundGain;
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

    public void Dispose()
    {
        _disposed = true;
        foreach (var voice in _voices.Values)
        {
            voice.Dispose();
        }

        _voices.Clear();
        if (_graph is { IsCompletedSuccessfully: true, Result: { } graph })
        {
            graph.Dispose();
        }
    }

    private async void OpenAsync(SourceImage image, Voice voice)
    {
        try
        {
            var graph = await (_graph ??= CreateGraphAsync());
            if (graph is null || _output is null || voice.IsDisposed)
            {
                return;
            }

            var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(image.FilePath!));
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

            // Silent until the frames play; looping with its video.
            node.Stop();
            node.LoopCount = null;
            node.AddOutgoingConnection(_output);
            voice.Node = node;
            UpdateGraph();
        }
        catch (Exception)
        {
            // No sound for this video in the preview; its frames still play.
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

    /// <summary>The graph runs while it has a video to mix, so an idle preview holds no audio stream open.</summary>
    private void UpdateGraph()
    {
        if (_disposed || _graph is not { IsCompletedSuccessfully: true, Result: { } graph })
        {
            return;
        }

        bool needed = _voices.Count > 0;
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

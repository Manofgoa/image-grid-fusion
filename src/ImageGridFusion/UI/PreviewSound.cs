using ImageGridFusion.Composition;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;

namespace ImageGridFusion.UI;

/// <summary>
/// Plays the sound of the grid's sound source (see <see cref="Animation.SoundSource"/>) with Windows'
/// media player, kept in step with the frames the preview shows. Used from the UI thread only.
/// </summary>
internal sealed class PreviewSound : IDisposable
{
    /// <summary>Beyond this gap between the sound and the frames, the sound jumps back in step.</summary>
    private static readonly TimeSpan Drift = TimeSpan.FromMilliseconds(250);

    private MediaPlayer? _player;

    /// <summary>Image whose sound plays; <c>null</c> for none.</summary>
    public SourceImage? Image { get; private set; }

    /// <summary>Switches to the sound of <paramref name="image"/>; <c>null</c> stops the sound.</summary>
    public async void Follow(SourceImage? image)
    {
        if (image == Image)
        {
            return;
        }

        Image = image;
        _player?.Dispose();
        _player = null;
        if (image?.FilePath is not { } path)
        {
            return;
        }

        try
        {
            var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(path));
            if (Image != image)
            {
                return;
            }

            var player = new MediaPlayer { AutoPlay = false, IsLoopingEnabled = true };

            // Not a media session of its own: no Windows media overlay for the preview.
            player.CommandManager.IsEnabled = false;
            player.Source = MediaSource.CreateFromStorageFile(file);
            _player = player;
        }
        catch (Exception)
        {
            // No sound in the preview; the frames still play.
        }
    }

    /// <summary>Plays from <paramref name="position"/> when <paramref name="playing"/>, else pauses.</summary>
    public void Sync(TimeSpan position, bool playing)
    {
        if (_player is null)
        {
            return;
        }

        var session = _player.PlaybackSession;
        switch (session.PlaybackState)
        {
            case MediaPlaybackState.Opening or MediaPlaybackState.Buffering:
                return;
            case MediaPlaybackState.Playing when !playing:
                _player.Pause();
                return;
            case MediaPlaybackState.Playing:
                if ((session.Position - position).Duration() > Drift)
                {
                    session.Position = position;
                }

                return;
            default:
                if (playing)
                {
                    session.Position = position;
                    _player.Play();
                }

                return;
        }
    }

    public void Dispose()
    {
        Image = null;
        _player?.Dispose();
        _player = null;
    }
}

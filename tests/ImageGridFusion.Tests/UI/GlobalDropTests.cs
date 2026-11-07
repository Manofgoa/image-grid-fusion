using ImageGridFusion.Composition;
using ImageGridFusion.Imaging;
using ImageGridFusion.UI;

namespace ImageGridFusion.Tests.UI;

/// <summary>What a file dropped onto the Global toolbar goes to (workfiles/20261107-soundtrack-drop-from-url-zone.md).</summary>
[TestClass]
public sealed class GlobalDropTests
{
    [TestMethod]
    public void TheSoundtrackTabTakesTheDropWhateverTabIsSelected()
    {
        foreach (GlobalEffect? selected in new GlobalEffect?[] { null, GlobalEffect.Format, GlobalEffect.Layers, GlobalEffect.Soundtrack })
        {
            Assert.AreEqual(GlobalDropTarget.Soundtrack, GlobalDrop.TargetOf(GlobalEffect.Soundtrack, overOptions: false, selected, exporting: false));
        }
    }

    [TestMethod]
    public void TheLayersTakeTheDropOnTheirTabOrTheirOptions()
    {
        Assert.AreEqual(GlobalDropTarget.Layers, GlobalDrop.TargetOf(GlobalEffect.Layers, overOptions: false, GlobalEffect.Borders, exporting: false));
        Assert.AreEqual(GlobalDropTarget.Layers, GlobalDrop.TargetOf(null, overOptions: true, GlobalEffect.Layers, exporting: false));
    }

    [TestMethod]
    public void TheSoundtrackOptionsTakeTheDropWhileSelected()
    {
        Assert.AreEqual(GlobalDropTarget.Soundtrack, GlobalDrop.TargetOf(null, overOptions: true, GlobalEffect.Soundtrack, exporting: false));
    }

    [TestMethod]
    public void TheGridBackgroundTakesTheDropOnItsTabOrItsOptions()
    {
        Assert.AreEqual(GlobalDropTarget.GridBackground, GlobalDrop.TargetOf(GlobalEffect.GridBackground, overOptions: false, null, exporting: false));
        Assert.AreEqual(GlobalDropTarget.GridBackground, GlobalDrop.TargetOf(null, overOptions: true, GlobalEffect.GridBackground, exporting: false));
    }

    [TestMethod]
    public void AnotherTabOrItsOptionsRefuseTheDrop()
    {
        foreach (var effect in new[] { GlobalEffect.Format, GlobalEffect.Fade, GlobalEffect.Borders, GlobalEffect.Seams, GlobalEffect.Cascade })
        {
            Assert.AreEqual(GlobalDropTarget.None, GlobalDrop.TargetOf(effect, overOptions: false, GlobalEffect.Soundtrack, exporting: false), $"{effect} tab");
            Assert.AreEqual(GlobalDropTarget.None, GlobalDrop.TargetOf(null, overOptions: true, effect, exporting: false), $"{effect} options");
        }
    }

    [TestMethod]
    public void TheOptionsWithNoTabSelectedAndTheRowsEmptyPartsRefuseTheDrop()
    {
        Assert.AreEqual(GlobalDropTarget.None, GlobalDrop.TargetOf(null, overOptions: true, null, exporting: false));
        Assert.AreEqual(GlobalDropTarget.None, GlobalDrop.TargetOf(null, overOptions: false, GlobalEffect.Soundtrack, exporting: false));
    }

    [TestMethod]
    public void EveryDropIsRefusedWhileExporting()
    {
        Assert.AreEqual(GlobalDropTarget.None, GlobalDrop.TargetOf(GlobalEffect.Soundtrack, overOptions: false, null, exporting: true));
        Assert.AreEqual(GlobalDropTarget.None, GlobalDrop.TargetOf(GlobalEffect.Layers, overOptions: false, null, exporting: true));
        Assert.AreEqual(GlobalDropTarget.None, GlobalDrop.TargetOf(null, overOptions: true, GlobalEffect.GridBackground, exporting: true));
    }

    [TestMethod]
    public void AudioAndVideoFilesMayCarrySoundWhateverTheirCase()
    {
        foreach (var path in new[] { @"C:\a\song.mp3", @"C:\a\voice.M4A", @"C:\a\x-post.mp4", @"C:\a\clip.WebM", "track.flac" })
        {
            Assert.IsTrue(SoundtrackFile.MayCarrySound(path), path);
        }
    }

    [TestMethod]
    public void StillsTextsAndFilesWithoutExtensionCarryNoSound()
    {
        foreach (var path in new[] { @"C:\a\photo.jpg", @"C:\a\shot.png", @"C:\a\sticker.gif", @"C:\a\notes.txt", @"C:\a\noextension", @"C:\a\dot." })
        {
            Assert.IsFalse(SoundtrackFile.MayCarrySound(path), path);
        }
    }

    [TestMethod]
    public void TheSoundtrackTakesTheFirstFileThatMayCarrySound()
    {
        Assert.AreEqual(@"C:\a\b.mp4", GlobalDrop.SoundtrackFileOf([@"C:\a\a.jpg", @"C:\a\b.mp4", @"C:\a\c.mp3"]));
        Assert.IsNull(GlobalDrop.SoundtrackFileOf([@"C:\a\a.jpg", @"C:\a\b.gif"]));
    }

    [TestMethod]
    public void TheSoundtrackRefusesADragWithoutSoundTheOthersTakeAnyFile()
    {
        string[] stills = [@"C:\a\a.jpg", @"C:\a\b.png"];

        Assert.IsFalse(GlobalDrop.Accepts(GlobalDropTarget.Soundtrack, stills));
        Assert.IsTrue(GlobalDrop.Accepts(GlobalDropTarget.Soundtrack, [@"C:\a\a.jpg", @"C:\a\b.mp4"]));
        Assert.IsTrue(GlobalDrop.Accepts(GlobalDropTarget.Layers, stills));
        Assert.IsTrue(GlobalDrop.Accepts(GlobalDropTarget.GridBackground, stills));
        Assert.IsFalse(GlobalDrop.Accepts(GlobalDropTarget.None, stills));
    }
}

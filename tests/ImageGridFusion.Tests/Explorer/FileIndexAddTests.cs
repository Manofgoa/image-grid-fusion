using ImageGridFusion.Explorer;

namespace ImageGridFusion.Tests.Explorer;

// A file saved under the base folder recorded at once, without a rescan
// (workfiles/20261107-save-downloaded-content.md § After Saving).
[TestClass]
public sealed class FileIndexAddTests
{
    private string _root = "";

    [TestInitialize]
    public void Create()
    {
        this._root = Directory.CreateTempSubdirectory("igf-index-").FullName;
        File.WriteAllText(Path.Combine(this._root, "kept.txt"), "kept");
    }

    [TestCleanup]
    public void Delete() => Directory.Delete(this._root, recursive: true);

    [TestMethod]
    public void ASavedFileIsAddedAlone()
    {
        var index = FileIndex.Scan(this._root, null, CancellationToken.None);
        Directory.CreateDirectory(Path.Combine(this._root, "sub"));
        string saved = Path.Combine(this._root, "sub", "clip.mp4");
        File.WriteAllBytes(saved, new byte[42]);

        Assert.IsTrue(index.Add(saved));

        Assert.AreEqual(2, index.Count);
        var entry = index.Entries.Single(e => e.RelativePath == Path.Combine("sub", "clip.mp4"));
        Assert.AreEqual(42L, entry.Size);
    }

    [TestMethod]
    public void AnOverwrittenFileReplacesItsEntry()
    {
        string saved = Path.Combine(this._root, "kept.txt");
        var index = FileIndex.Scan(this._root, null, CancellationToken.None);
        File.WriteAllText(saved, "longer content");

        Assert.IsTrue(index.Add(saved));

        Assert.AreEqual(1, index.Count);
        Assert.AreEqual(14L, index.Entries[0].Size);
    }

    [TestMethod]
    public void AFileOutsideTheFolderIsNotAdded()
    {
        var index = FileIndex.Scan(this._root, null, CancellationToken.None);
        string outside = Path.Combine(Path.GetTempPath(), $"igf-outside-{Guid.NewGuid():N}.txt");
        File.WriteAllText(outside, "x");
        try
        {
            Assert.IsFalse(index.Add(outside));
            Assert.IsFalse(index.Add(Path.Combine(this._root, "missing.txt")));
            Assert.AreEqual(1, index.Count);
        }
        finally
        {
            File.Delete(outside);
        }
    }
}

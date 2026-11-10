using ImageGridFusion.Explorer;

namespace ImageGridFusion.Tests.Explorer;

// 🔄 inside a subfolder of a secondary source reads that subfolder alone and merges it into the source's index
// (workfiles/20261110-secondary-source-subfolder-rescan.md § Scan of a Subfolder).
[TestClass]
public sealed class FileIndexSubfolderScanTests
{
    private string _root = "";

    [TestInitialize]
    public void Create()
    {
        this._root = Directory.CreateTempSubdirectory("igf-subscan-").FullName;
        this.Write("top.txt");
        this.Write(Path.Combine("a", "b", "one.txt"));
        this.Write(Path.Combine("a", "b", "deep", "two.txt"));
        this.Write(Path.Combine("a", "bc", "sibling.txt"));
        this.Write(Path.Combine("other", "three.txt"));
    }

    [TestCleanup]
    public void Delete()
    {
        foreach (var file in Directory.EnumerateFiles(this._root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(this._root, recursive: true);
    }

    [TestMethod]
    public void ASubfolderScanRecordsItsFilesOnlyAgainstTheRoot()
    {
        var scanned = FileIndex.Scan(this._root, Path.Combine("a", "b"), null, CancellationToken.None);

        CollectionAssert.AreEquivalent(
            new[] { Path.Combine("a", "b", "one.txt"), Path.Combine("a", "b", "deep", "two.txt") },
            scanned.Entries.Select(e => e.RelativePath).ToArray());
        Assert.AreEqual(FileIndex.NormalizeFolder(this._root), scanned.BaseFolder);
    }

    [TestMethod]
    public void ASubfolderScanSkipsHiddenFiles()
    {
        string hidden = this.Write(Path.Combine("a", "b", "hidden.txt"));
        File.SetAttributes(hidden, FileAttributes.Hidden);

        var scanned = FileIndex.Scan(this._root, Path.Combine("a", "b"), null, CancellationToken.None);

        Assert.IsFalse(scanned.Entries.Any(e => e.RelativePath.EndsWith("hidden.txt", StringComparison.Ordinal)));
        Assert.AreEqual(2, scanned.Count);
    }

    [TestMethod]
    public void TheMergeReplacesTheSubfolderAndKeepsTheRest()
    {
        var index = FileIndex.Scan(this._root, null, CancellationToken.None);
        File.Delete(Path.Combine(this._root, "a", "b", "one.txt"));
        this.Write(Path.Combine("a", "b", "new.txt"));
        // Changed outside the subfolder: the merge must not see it.
        this.Write(Path.Combine("other", "unseen.txt"));

        string folder = Path.Combine("a", "b");
        var merged = index.WithFolder(folder, FileIndex.Scan(this._root, folder, null, CancellationToken.None));

        CollectionAssert.AreEquivalent(
            new[]
            {
                "top.txt",
                Path.Combine("a", "b", "new.txt"),
                Path.Combine("a", "b", "deep", "two.txt"),
                Path.Combine("a", "bc", "sibling.txt"),
                Path.Combine("other", "three.txt"),
            },
            merged.Entries.Select(e => e.RelativePath).ToArray());
        var kept = index.Entries.Single(e => e.RelativePath == "top.txt");
        Assert.AreSame(kept, merged.Entries.Single(e => e.RelativePath == "top.txt"));
    }

    [TestMethod]
    public void ASiblingSharingTheNameIsNotPartOfTheSubfolder()
    {
        var index = FileIndex.Scan(this._root, null, CancellationToken.None);
        File.Delete(Path.Combine(this._root, "a", "bc", "sibling.txt"));

        string folder = Path.Combine("a", "b");
        var merged = index.WithFolder(folder, FileIndex.Scan(this._root, folder, null, CancellationToken.None));

        Assert.IsTrue(merged.Entries.Any(e => e.RelativePath == Path.Combine("a", "bc", "sibling.txt")));
    }

    [TestMethod]
    public void TheMergeKeepsTheScanDate()
    {
        var index = FileIndex.Scan(this._root, null, CancellationToken.None);
        string folder = Path.Combine("a", "b");
        var scanned = FileIndex.Scan(this._root, folder, null, CancellationToken.None);

        var merged = index.WithFolder(folder, scanned);

        Assert.AreEqual(index.ScannedAt, merged.ScannedAt);
    }

    [TestMethod]
    public void AScanOfAnotherFolderIsRefused()
    {
        var index = FileIndex.Scan(this._root, null, CancellationToken.None);
        var other = FileIndex.Scan(Path.Combine(this._root, "other"), null, CancellationToken.None);

        Assert.ThrowsExactly<ArgumentException>(() => index.WithFolder("x", other));
    }

    private string Write(string relative)
    {
        string path = Path.Combine(this._root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, relative);
        return path;
    }
}

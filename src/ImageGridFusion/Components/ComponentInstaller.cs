using System.Security.Cryptography;
using System.Text;
using ImageGridFusion.Explorer;
using ImageGridFusion.Web;

namespace ImageGridFusion.Components;

/// <summary>Where an install stands: its share done, unknown for a winget install, and what to say about it.</summary>
internal sealed record InstallProgress(double? Share, string Text);

/// <summary>
/// The install of a component's version, in two steps: its files downloaded into a staging folder and checked, off the UI
/// thread; then swapped with the version installed — the files of the current version deleted, the new ones moved in —
/// once the caller has released what holds them. A file still locked (a native runtime loaded) stays staged: the swap is
/// finished at the next start, before anything loads it (<see cref="ApplyPending"/>). A winget tool is installed by winget,
/// one at a time. See workfiles/20261102-component-update-verification.md § Updating.
/// </summary>
internal static class ComponentInstaller
{
    /// <summary>The staging folder in a component's folder, a subfolder per component.</summary>
    public const string StagingFolderName = "update-staging";

    // A tool's folder set aside while winget installs its new version into its place, given back if it fails.
    private const string PreviousSuffix = "-previous";

    // A file moved into place is tried again this often, so a session closing a few ms late does not stage it.
    private const int MoveAttempts = 5;
    private const int MoveRetryMs = 200;

    // winget refuses concurrent installs: one at a time.
    private static readonly SemaphoreSlim WingetGate = new(1, 1);

    public static string StagingOf(Component component) => Path.Combine(component.Folder, StagingFolderName, component.Id);

    /// <summary>
    /// Downloads <paramref name="target"/>'s files into <paramref name="component"/>'s staging folder, each checked against
    /// its SHA-256 when it has one and by the component's own check; returns the version with every file's SHA-256. Throws a
    /// <see cref="UrlLoadException"/> saying why when it cannot, the staging folder deleted.
    /// </summary>
    public static async Task<ComponentVersion> DownloadAsync(Component component, ComponentVersion target, IProgress<InstallProgress> progress, CancellationToken token)
    {
        string staging = StagingOf(component);
        DeleteFolder(staging);
        Directory.CreateDirectory(staging);
        try
        {
            var zips = new Dictionary<string, IReadOnlyDictionary<string, RemoteZipEntry>>();
            var sizes = new List<long>();
            foreach (var file in target.Files)
            {
                if (file.ZipEntry is { } entry)
                {
                    if (!zips.TryGetValue(file.Url, out var directory))
                    {
                        progress.Report(new InstallProgress(0, "Reading the package…"));
                        directory = await RemoteZip.ReadDirectoryAsync(new Uri(file.Url), token);
                        zips[file.Url] = directory;
                    }

                    sizes.Add(directory.TryGetValue(entry, out var found) ? found.CompressedSize : throw new UrlLoadException($"the package has no {entry}"));
                }
                else
                {
                    sizes.Add(file.Size);
                }
            }

            long total = Math.Max(1, sizes.Sum());
            long before = 0;
            var files = new List<ComponentFile>();
            for (int i = 0; i < target.Files.Count; i++)
            {
                var file = target.Files[i];
                string path = Path.Combine(staging, file.Name);
                long start = before;
                long weight = sizes[i];
                void Report(double share)
                {
                    long done = start + (long)(weight * share);
                    progress.Report(new InstallProgress((double)done / total, $"{Megabytes(done)} / {Megabytes(total)} MB"));
                }

                if (file.ZipEntry is { } entry)
                {
                    var zipEntry = zips[file.Url][entry];
                    await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                    await RemoteZip.ExtractAsync(new Uri(file.Url), zipEntry, stream, new SyncProgress(count => Report(Math.Min(1, (double)count / Math.Max(1, zipEntry.CompressedSize)))), token);
                }
                else
                {
                    await DirectEngineDownload.SaveAsync(new Uri(file.Url), path, Report, token);
                }

                before += weight;
                progress.Report(new InstallProgress((double)before / total, "Checking…"));
                string sha256 = DirectEngineDownload.Sha256Of(path);
                if (file.Sha256 is { } expected && !string.Equals(sha256, expected, StringComparison.OrdinalIgnoreCase))
                {
                    throw new UrlLoadException($"{file.Name} does not match its expected content");
                }

                if (component.Accepts is { } accepts && !accepts(path))
                {
                    throw new UrlLoadException($"{file.Name} is not what {component.Name} needs");
                }

                files.Add(file with { Sha256 = sha256.ToLowerInvariant(), Size = new FileInfo(path).Length });
            }

            return target with { Files = files };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or HttpRequestException or InvalidDataException or UrlLoadException or OperationCanceledException)
        {
            DeleteFolder(staging);
            if (e is OperationCanceledException && token.IsCancellationRequested)
            {
                throw;
            }

            throw e as UrlLoadException ?? new UrlLoadException(e switch
            {
                HttpRequestException or OperationCanceledException => "The download broke off",
                InvalidDataException => "The package arrived damaged",
                _ => $"Couldn't save it: {e.Message}",
            }, e);
        }
    }

    /// <summary>
    /// Moves <paramref name="staged"/>'s files from the staging folder into place, then deletes the files of
    /// <paramref name="installed"/> the new version no longer has. Returns false when a file is still locked: it stays staged,
    /// the swap finished at the next start.
    /// </summary>
    public static bool Swap(Component component, ComponentVersion staged, ComponentVersion? installed)
    {
        string staging = StagingOf(component);
        bool done = true;
        foreach (var file in staged.Files)
        {
            string from = Path.Combine(staging, file.Name);
            if (File.Exists(from) && !TryMove(from, component.PathOf(file.Name)))
            {
                done = false;
            }
        }

        if (!done)
        {
            return false;
        }

        foreach (var old in installed?.Files ?? [])
        {
            if (!staged.Files.Any(f => string.Equals(f.Name, old.Name, StringComparison.OrdinalIgnoreCase)))
            {
                DirectEngineDownload.TryDelete(component.PathOf(old.Name));
            }
        }

        DeleteFolder(staging);
        DeleteEmptyFolder(Path.Combine(component.Folder, StagingFolderName));
        return true;
    }

    /// <summary>
    /// Installs <paramref name="version"/> of a winget tool into its folder, the folder there set aside meanwhile and given
    /// back if winget fails. Waits while another winget install runs, saying so. Throws a <see cref="UrlLoadException"/>
    /// saying why when it cannot.
    /// </summary>
    public static async Task InstallToolAsync(WingetTool tool, string version, IProgress<InstallProgress> progress, CancellationToken token)
    {
        if (WingetGate.CurrentCount == 0)
        {
            progress.Report(new InstallProgress(null, "Waiting for winget…"));
        }

        await WingetGate.WaitAsync(token);
        try
        {
            progress.Report(new InstallProgress(null, "Installing with winget…"));
            string folder = tool.Folder;
            string previous = folder + PreviousSuffix;
            DeleteFolder(previous);
            if (Directory.Exists(folder))
            {
                try
                {
                    Directory.Move(folder, previous);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    throw new UrlLoadException($"{tool.Name} is in use: try again once the URL load ends", e);
                }
            }

            try
            {
                await tool.InstallAsync(version, token);
                DeleteFolder(previous);
            }
            catch
            {
                DeleteFolder(folder);
                if (Directory.Exists(previous))
                {
                    Directory.Move(previous, folder);
                }

                throw;
            }
        }
        finally
        {
            WingetGate.Release();
        }
    }

    /// <summary>
    /// The swaps left for this start: each pending version's staged files moved into place, then recorded as installed.
    /// Called in <c>Program.Main</c> before anything loads a native runtime. Returns the names of the components swapped.
    /// </summary>
    public static IReadOnlyList<string> ApplyPending(ComponentManifest manifest, IEnumerable<Component> components)
    {
        var swapped = new List<string>();
        foreach (var component in components)
        {
            var record = manifest[component];
            if (record.Pending is not { } pending || !Swap(component, pending, record.Installed))
            {
                continue;
            }

            record.Installed = pending;
            record.Pending = null;
            record.InstalledOn ??= DateTimeOffset.Now;
            record.UpdatedOn = DateTimeOffset.Now;
            swapped.Add(component.Name);
        }

        if (swapped.Count > 0)
        {
            try
            {
                manifest.Save();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }

        return swapped;
    }

    /// <summary>The Git blob id of a file — what Hugging Face gives for its files out of LFS.</summary>
    public static string GitBlobOf(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        sha1.AppendData(Encoding.ASCII.GetBytes($"blob {stream.Length}\0"));
        var buffer = new byte[81920];
        int read;
        while ((read = stream.Read(buffer)) > 0)
        {
            sha1.AppendData(buffer.AsSpan(0, read));
        }

        return Convert.ToHexString(sha1.GetHashAndReset()).ToLowerInvariant();
    }

    private static bool TryMove(string from, string to)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(from, to, overwrite: true);
                return true;
            }
            catch (Exception e) when ((e is IOException or UnauthorizedAccessException) && attempt < MoveAttempts)
            {
                Thread.Sleep(MoveRetryMs);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }
    }

    private static void DeleteFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void DeleteEmptyFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
            {
                Directory.Delete(folder);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static string Megabytes(long bytes) => (bytes / 1e6).ToString(bytes < 10_000_000 ? "0.0" : "0", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>A progress reported on the reporting thread itself.</summary>
    private sealed class SyncProgress(Action<long> handler) : IProgress<long>
    {
        public void Report(long value) => handler(value);
    }
}

using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ImageGridFusion.UI;

/// <summary>
/// One running instance per exe location and Windows session: a named mutex marks it, and a named pipe carries
/// what a later launch of the same exe hands over to it — its files — before that launch exits.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    /// <summary>Starts an instance of its own, outside the lock: for tests while another instance runs.</summary>
    public const string NewInstanceArgument = "--new-instance";

    // How long a later launch waits for the running instance to answer before giving up.
    private const int HandOverTimeoutMs = 3000;

    // A failed pipe (a client gone mid-message, a name taken) is listened to again after this pause.
    private const int RelistenDelayMs = 1000;

    private readonly Mutex? _mutex;
    private readonly CancellationTokenSource _stop = new();

    private SingleInstance(Mutex? mutex) => this._mutex = mutex;

    /// <summary>
    /// Takes the lock for this exe: the instance returned is the running one. Null when another instance of the
    /// same exe already runs. If the lock cannot be made at all, the app runs without it rather than not at all.
    /// </summary>
    public static SingleInstance? TryAcquire()
    {
        try
        {
            var mutex = new Mutex(initiallyOwned: true, $@"Local\{Key}", out bool created);
            if (created)
            {
                return new SingleInstance(mutex);
            }

            mutex.Dispose();
            return null;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or WaitHandleCannotBeOpenedException)
        {
            return new SingleInstance(null);
        }
    }

    /// <summary>
    /// Hands the files over to the running instance, which comes to the front and loads them. Silent when it does
    /// not answer in time (hung, quitting): the launch then ends with nothing done.
    /// </summary>
    public static void HandOver(IEnumerable<string> files)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            pipe.Connect(HandOverTimeoutMs);

            // Just launched by the user, this process may take the foreground: it lends that right to the running
            // instance, else its window would only flash in the taskbar.
            if (GetNamedPipeServerProcessId(pipe.SafePipeHandle, out uint runningId))
            {
                AllowSetForegroundWindow(runningId);
            }

            using var writer = new StreamWriter(pipe, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            foreach (var file in files)
            {
                writer.Write(FullPath(file));
                writer.Write('\n');
            }
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// Listens for later launches of the same exe until disposed; <paramref name="launched"/> gets each one's
    /// files, on the thread this is called from (the UI thread). Nothing to listen to without the lock.
    /// </summary>
    public void Listen(Action<string[]> launched)
    {
        if (this._mutex is null)
        {
            return;
        }

        var ui = SynchronizationContext.Current ?? throw new InvalidOperationException("Listen is called from the UI thread.");
        var stop = this._stop.Token;
        _ = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    using var pipe = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await pipe.WaitForConnectionAsync(stop);
                    using var reader = new StreamReader(pipe, Encoding.UTF8);
                    string message = await reader.ReadToEndAsync(stop);
                    string[] files = message.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    ui.Post(_ => launched(files), null);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    try
                    {
                        await Task.Delay(RelistenDelayMs, stop);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            }
        });
    }

    public void Dispose()
    {
        this._stop.Cancel();
        if (this._mutex is not null)
        {
            this._mutex.ReleaseMutex();
            this._mutex.Dispose();
        }

        this._stop.Dispose();
    }

    /// <summary>The lock's name: the exe's full path, case-insensitive, hashed to fit a kernel object's name.</summary>
    private static string Key
    {
        get
        {
            string exe = Path.GetFullPath(Environment.ProcessPath ?? Application.ExecutablePath).ToUpperInvariant();
            return "ImageGridFusion-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(exe)))[..16];
        }
    }

    // Pipe names are machine-wide, unlike the mutex's Local\ namespace: the session keeps two users apart.
    private static string PipeName
    {
        get
        {
            using var process = Process.GetCurrentProcess();
            return $"{Key}-{process.SessionId}";
        }
    }

    /// <summary>A file named relative to this launch's folder, made absolute for the running instance; left as is if it is no valid path.</summary>
    private static string FullPath(string file)
    {
        try
        {
            return Path.GetFullPath(file);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return file;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint serverProcessId);

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(uint processId);
}

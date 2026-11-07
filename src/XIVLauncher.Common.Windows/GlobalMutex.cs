using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XIVLauncher.Common.Windows;

/// <summary>
/// A thread-safe wrapper for creating and managing global mutexes in .NET Core.
/// Ensures only one instance of an application can run at a time.
/// </summary>
public sealed class GlobalMutex : IDisposable
{
    private readonly Thread ownerThread;
    private readonly ManualResetEventSlim releaseSignal = new();
    private bool disposed;

    /// <summary>
    /// Gets whether this instance successfully acquired the mutex (i.e., is the first/only instance).
    /// </summary>
    public bool IsOwned { get; }

    /// <summary>
    /// Creates a new global mutex using the specified identifier.
    /// </summary>
    /// <param name="identifier">A string identifier that will be hashed to create a unique mutex name.</param>
    /// <exception cref="ArgumentNullException">Thrown when identifier is null or empty.</exception>
    public GlobalMutex(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentNullException(nameof(identifier), "Identifier cannot be null or empty.");
        }

        var mutexName = GenerateMutexName(identifier);

        var acquired = new TaskCompletionSource<bool>();

        this.ownerThread = new Thread(() =>
        {
            Mutex mutex;
            bool owned;

            try
            {
                mutex = new Mutex(true, mutexName, out var createdNew);
                owned = createdNew || TryAcquire(mutex);
            }
            catch (Exception ex)
            {
                acquired.SetException(ex);
                return;
            }

            acquired.SetResult(owned);

            if (!owned)
            {
                mutex.Dispose();
                return;
            }

            this.releaseSignal.Wait();
            mutex.ReleaseMutex();
            mutex.Dispose();
        })
        {
            IsBackground = true,
            Name = "GlobalMutex owner",
        };

        this.ownerThread.Start();

        // This rethrows
        this.IsOwned = acquired.Task.GetAwaiter().GetResult();
    }

    private static bool TryAcquire(Mutex mutex)
    {
        // Mutex already exists, try to acquire it with zero timeout
        try
        {
            return mutex.WaitOne(0, false);
        }
        catch (AbandonedMutexException)
        {
            // Previous owner terminated without releasing - we now own it
            return true;
        }
    }

    private static string GenerateMutexName(string identifier)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(identifier));
        var sb = new StringBuilder(hashBytes.Length * 2);

        foreach (var b in hashBytes)
        {
            sb.Append(b.ToString("x2"));
        }

        // Prefix with "Global\\" to make it system-wide across sessions
        return $"Global\\XIVLAUNCHER{sb}";
    }

    /// <summary>
    /// Releases the mutex if owned.
    /// </summary>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;

        // Wait for release
        this.releaseSignal.Set();
        this.ownerThread.Join();
        this.releaseSignal.Dispose();
    }
}

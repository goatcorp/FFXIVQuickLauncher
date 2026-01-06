using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace XIVLauncher.Common.Windows;

/// <summary>
/// A thread-safe wrapper for creating and managing global mutexes in .NET Core.
/// Ensures only one instance of an application can run at a time.
/// </summary>
public sealed class GlobalMutex : IDisposable
{
    private readonly Mutex? mutex;
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

        try
        {
            // Try to create the mutex with initial ownership
            this.mutex = new Mutex(true, mutexName, out var createdNew);

            if (createdNew)
            {
                // We created and own the mutex
                this.IsOwned = true;
            }
            else
            {
                // Mutex already exists, try to acquire it with zero timeout
                try
                {
                    this.IsOwned = this.mutex.WaitOne(0, false);
                }
                catch (AbandonedMutexException)
                {
                    // Previous owner terminated without releasing - we now own it
                    this.IsOwned = true;
                }
            }
        }
        catch (Exception)
        {
            // If anything goes wrong, clean up and rethrow
            this.mutex?.Dispose();
            throw;
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

        if (this.IsOwned && this.mutex != null)
        {
            try
            {
                this.mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Mutex was already released or not owned - safe to ignore
            }
        }

        this.mutex?.Dispose();
        this.disposed = true;
    }
}

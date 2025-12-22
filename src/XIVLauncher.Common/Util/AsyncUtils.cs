using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace XIVLauncher.Common.Util;

/// <summary>
/// A set of utilities around and for better asynchronous behavior.
/// </summary>
public static class AsyncUtils
{
    /// <summary>
    /// Race a set of tasks, returning either the first to succeed or an aggregate of all exceptions. This helper does
    /// not perform any automatic cancellation of losing tasks, nor does it handle exceptions of losing tasks.
    /// </summary>
    /// <remarks>Derived from <a href="https://stackoverflow.com/a/37529395">this StackOverflow post</a>.</remarks>
    /// <param name="tasks">A list of tasks to race.</param>
    /// <typeparam name="T">The return type of all raced tasks.</typeparam>
    /// <exception cref="AggregateException">Thrown when all tasks given to this method fail.</exception>
    /// <returns>Returns the first task that completes, according to <see cref="Task.IsCompletedSuccessfully"/>.</returns>
    public static Task<T> FirstSuccessfulTask<T>(ICollection<Task<T>> tasks)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var remainingTasks = tasks.Count;

        foreach (var task in tasks)
        {
            task.ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully)
                {
                    tcs.TrySetResult(t.Result);
                }
                else if (Interlocked.Decrement(ref remainingTasks) == 0)
                {
                    tcs.SetException(new AggregateException(
                        tasks.SelectMany(f => f.Exception?.InnerExceptions ?? Enumerable.Empty<Exception>())));
                }
            });
        }

        return tcs.Task;
    }

    /// <summary>
    /// Provide a <see cref="Task.Delay(int, CancellationToken)"/> that won't throw an exception when it's canceled.
    /// </summary>
    /// <inheritdoc cref="Task.Delay(int, CancellationToken)"/>
    public static async Task CancellableDelay(int millisecondsDelay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(millisecondsDelay, cancellationToken).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
        }
    }
}

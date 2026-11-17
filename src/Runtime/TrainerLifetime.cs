using System;
using System.Collections.Generic;

// Releases owned resources in reverse acquisition order, including partial initialization.
internal sealed class TrainerLifetime : IDisposable
{
    private readonly Stack<Action> cleanup = new();
    private readonly Action<Exception> reportFailure;
    private bool disposed;

    internal TrainerLifetime(Action<Exception> reportFailure)
    {
        this.reportFailure = reportFailure ?? throw new ArgumentNullException(nameof(reportFailure));
    }

    internal void Add(Action release)
    {
        if (disposed) throw new ObjectDisposedException(nameof(TrainerLifetime));
        cleanup.Push(release ?? throw new ArgumentNullException(nameof(release)));
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        while (cleanup.Count > 0)
        {
            try { cleanup.Pop()(); }
            catch (Exception error) { reportFailure(error); }
        }
    }
}

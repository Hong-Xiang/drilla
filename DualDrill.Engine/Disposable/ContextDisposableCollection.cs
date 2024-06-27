using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Engine.Disposable;

public sealed class ContextDisposableCollection : IDisposable
{
    private readonly ConcurrentStack<IDisposable> Disposables = [];
    private bool disposed;

    public void Add(IDisposable disposable)
    {
        Disposables.Push(disposable);
    }

    private void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                foreach (var disposable in Disposables)
                {
                    disposable.Dispose();
                }
            }
            disposed = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

public sealed class ContextAsyncDisposableCollection : IAsyncDisposable
{
    private readonly ConcurrentStack<IAsyncDisposable> Disposables = [];

    public void Add(IAsyncDisposable disposable)
    {
        Disposables.Push(disposable);
    }

    public void Add(IDisposable disposable)
    {
        Disposables.Push(new DisposableAsyncWrapper(disposable));
    }

    sealed record class DisposableAsyncWrapper(IDisposable Disposable) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            Disposable.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var disposable in Disposables)
        {
            await disposable.DisposeAsync().ConfigureAwait(false);
        }
    }
}

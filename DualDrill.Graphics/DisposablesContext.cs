using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics;

sealed class DisposablesContext : IDisposable
{
    List<IDisposable> Disposables { get; } = [];

    public T Add<T>(T disposable)
        where T : IDisposable
    {
        Disposables.Add(disposable);
        return disposable;
    }

    public void Dispose()
    {
        foreach (var disposable in Disposables)
        {
            disposable.Dispose();
        }
    }
}

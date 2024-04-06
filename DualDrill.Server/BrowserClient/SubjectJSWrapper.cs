using Microsoft.JSInterop;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace DualDrill.Server.BrowserClient;

public sealed class SubjectJSWrapper<T>(Subject<T> Value) : IDisposable
{
    [JSInvokable]
    public void OnNext(T value)
    {
        Value.OnNext(value);
    }

    public IObservable<T> Observable = Value;

    public void Dispose()
    {
        Value.Dispose();
    }
}

public sealed class TaskCompletionSourceJSWrapper<T>(TaskCompletionSource<T> Value)
{
    [JSInvokable]
    public void SetResult(T value) { Value.SetResult(value); }

    public Task<T> Task = Value.Task;
}

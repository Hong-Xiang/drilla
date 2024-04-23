using Microsoft.JSInterop;

namespace DualDrill.Server.BrowserClient;

public sealed class JSPromiseLikeBuilder<T>(Func<T, Task> ResolveAction, Func<string, Task> RejectAction)
{
    public DotNetObjectReference<JSPromiseLikeBuilder<T>> CreateReference() => DotNetObjectReference.Create(this);

    [JSInvokable]
    public Task Resolve(T value)
    {
        return ResolveAction(value);
    }

    [JSInvokable]
    public Task Reject(string message)
    {
        return RejectAction(message);
    }

}

public sealed class JSPromiseLikeBuilder<T1, T2>(Func<T1, T2, Task> ResolveAction, Func<string, Task> RejectAction)
{
    [JSInvokable]
    public Task Resolve(T1 v1, T2 v2)
    {
        return ResolveAction(v1, v2);
    }

    [JSInvokable]
    public Task Reject(string message)
    {
        return RejectAction(message);
    }
}

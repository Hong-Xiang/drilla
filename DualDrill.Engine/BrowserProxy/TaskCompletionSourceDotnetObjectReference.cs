using Microsoft.JSInterop;

namespace DualDrill.Engine.BrowserProxy;

sealed class JSTaskRejectException(string Message) : Exception(Message)
{
}

public sealed class TaskCompletionSourceDotnetObjectReference<T> : IDisposable
{
    public TaskCompletionSource<T> TaskCompletionSource { get; } = new();
    public Task<T> Task => TaskCompletionSource.Task;

    public DotNetObjectReference<TaskCompletionSourceDotnetObjectReference<T>> Reference { get; }

    public TaskCompletionSourceDotnetObjectReference()
    {
        Reference = DotNetObjectReference.Create(this);
    }

    [JSInvokable]
    public void Resolve(T value) { TaskCompletionSource.SetResult(value); }
    [JSInvokable]
    public void Reject(string message)
    {
        TaskCompletionSource.SetException(new JSTaskRejectException(message));
    }

    public void Dispose()
    {
        Reference.Dispose();
    }
}

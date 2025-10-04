using Microsoft.JSInterop;
using System.Reactive.Subjects;

namespace DualDrill.Engine.BrowserProxy;

public sealed class SubjectReferenceWrapper<T> : IDisposable
{
    Subject<T> Value { get; }
    public IObservable<T> Observable => Value;
    public DotNetObjectReference<SubjectReferenceWrapper<T>> Reference { get; }
    public SubjectReferenceWrapper(Subject<T> subject)
    {
        Value = subject;
        Reference = DotNetObjectReference.Create(this);
    }

    [JSInvokable]
    public void OnNext(T value)
    {
        Value.OnNext(value);
    }

    public void Dispose()
    {
        Value.Dispose();
        Reference.Dispose();
    }
}

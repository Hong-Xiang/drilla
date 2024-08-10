using DualDrill.Common;
using MessagePipe;
using System.Reactive.Disposables;

namespace DualDrill.Engine;

public static class MessagePipeExtensions
{
    public static (IDisposable, IAsyncSubscriber<T>) FromDotNetEventAsync<T>(
    this EventFactory eventFactory,
    Action<Action<T>> addHandler,
    Action<Action<T>> removeHandler
)
    {
        var (publisher, subscriber) = eventFactory.CreateAsyncEvent<T>();
        void handler(T e)
        {
            publisher.Publish(e);
        }
        addHandler(handler);
        return (Disposable.Create(() =>
        {
            removeHandler(handler);
            publisher.Dispose();
        }), subscriber);
    }

    public static (IDisposable, IAsyncSubscriber<Unit>) FromDotNetEventAsync(
        this EventFactory eventFactory,
        Action<Action> addHandler,
        Action<Action> removeHandler
    )
    {
        var (publisher, subscriber) = eventFactory.CreateAsyncEvent<Unit>();
        void handler()
        {
            publisher.Publish(default);
        }
        addHandler(handler);
        return (Disposable.Create(() =>
        {
            removeHandler(handler);
            publisher.Dispose();
        }), subscriber);
    }

    public static IAsyncSubscriber<Unit> FromDotNetEventAsync(
            this EventFactory eventFactory,
            Action<Action> addHandler,
            Action<Action> removeHandler,
            ICollection<IDisposable> disposables
        )
    {
        var (disposable, subscriber) = FromDotNetEventAsync(eventFactory, addHandler, removeHandler);
        disposables.Add(disposable);
        return subscriber;
    }

    public static IAsyncSubscriber<T> FromDotNetEventAsync<T>(
                this EventFactory eventFactory,
                Action<Action<T>> addHandler,
                Action<Action<T>> removeHandler,
                ICollection<IDisposable> disposables
            )
    {
        var (disposable, subscriber) = FromDotNetEventAsync(eventFactory, addHandler, removeHandler);
        disposables.Add(disposable);
        return subscriber;
    }
}

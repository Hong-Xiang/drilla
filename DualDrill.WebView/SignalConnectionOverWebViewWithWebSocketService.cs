using DualDrill.Engine;
using DualDrill.Engine.Connection;
using DualDrill.Engine.Event;
using DualDrill.WebView.Event;
using MessagePipe;
using System.Reactive.Disposables;

namespace DualDrill.WebView;

public sealed class SignalConnectionOverWebViewWithWebSocketService(
    IWebViewService WebViewService,
    IAsyncSubscriber<Guid, ConnectionEvent<AddIceCandidateEvent>> AddIceCandidateToClientSub,
    IAsyncSubscriber<Guid, ConnectionEvent<OfferEvent>> OfferToClientSub,
    IAsyncSubscriber<Guid, ConnectionEvent<AnswerEvent>> AnswerToClientSub,
    IAsyncPublisher<Guid, ConnectionEvent<AddIceCandidateEvent>> AddIceCandidateToClientPub,
    IAsyncPublisher<Guid, ConnectionEvent<OfferEvent>> OfferToClientPub,
    IAsyncPublisher<Guid, ConnectionEvent<AnswerEvent>> AnswerToClientPub) : ISignalConnectionProviderService
{
    private Guid ServerId => ClientsManager.ServerId;

    public async ValueTask AddIceCandidateAsync(ConnectionEvent<AddIceCandidateEvent> e, CancellationToken cancellation)
    {
        if (e.TargetId == ClientsManager.ServerId)
        {
            await WebViewService.SendMessageAsync(e, cancellation);
        }
        else
        {
            await AddIceCandidateToClientPub.PublishAsync(e.TargetId, e, cancellation);
        }
    }

    public async ValueTask OfferAsync(ConnectionEvent<OfferEvent> e, CancellationToken cancellation)
    {
        if (e.TargetId == ClientsManager.ServerId)
        {
            await WebViewService.SendMessageAsync(e, cancellation);
        }
        else
        {
            await OfferToClientPub.PublishAsync(e.TargetId, e, cancellation);
        }
    }

    public async ValueTask AnswerAsync(ConnectionEvent<AnswerEvent> e, CancellationToken cancellation)
    {
        if (e.TargetId == ClientsManager.ServerId)
        {
            await WebViewService.SendMessageAsync(e, cancellation);
        }
        else
        {
            await AnswerToClientPub.PublishAsync(e.TargetId, e, cancellation);
        }
    }

    public IDisposable SubscribeOfferAwait(Guid target, Func<ConnectionEvent<OfferEvent>, CancellationToken, ValueTask> handler)
    {
        if (target == ServerId)
        {
            throw new NotSupportedException("Subscribe to server event is not supported");
        }
        return OfferToClientSub.Subscribe(
            target,
            async (e, cancellation) =>
            {
                await handler(e, cancellation);
            }
        );
    }

    public IDisposable SubscribeAnswerAwait(Guid target, Func<ConnectionEvent<AnswerEvent>, CancellationToken, ValueTask> handler)
    {
        if (target == ClientsManager.ServerId)
        {
            throw new NotSupportedException("Subscribe to server event is not supported");
        }
        return AnswerToClientSub.Subscribe(
            target,
            async (e, cancellation) =>
            {
                await handler(e, cancellation);
            }
        );
    }

    public IDisposable SubscribeAddIceCandidateAwait(Guid target, Func<ConnectionEvent<AddIceCandidateEvent>, CancellationToken, ValueTask> handler)
    {
        if (target == ServerId)
        {
            throw new NotSupportedException("Subscribe to server event is not supported");
        }
        return AddIceCandidateToClientSub.Subscribe(
            target,
            async (e, cancellation) =>
            {
                await handler(e, cancellation);
            }
        );
    }
}

using DualDrill.Engine;
using DualDrill.Engine.Connection;
using DualDrill.WebView.Event;
using MessagePipe;
using System.Reactive.Disposables;

namespace DualDrill.WebView;

public sealed class SignalConnectionServiceOverWebViewService : ISignalConnectionProviderService
{
    private readonly IWebViewService WebViewService;

    public SignalConnectionServiceOverWebViewService(
        IWebViewService webViewService,
        EventFactory eventFactory
    )
    {
        WebViewService = webViewService;

        (OfferToClientPub, OfferToClientSub) = eventFactory.CreateEvent<ClientEvent<OfferPayload>>();
        (AnswerToClientPub, AnswerToClientSub) = eventFactory.CreateEvent<ClientEvent<AnswerPayload>>();
        (AddIceCandidateToClientPub, AddIceCandidateToClientSub) = eventFactory.CreateEvent<ClientEvent<AddIceCandidatePayload>>();
    }

    IPublisher<ClientEvent<AddIceCandidatePayload>> AddIceCandidateToClientPub;
    IPublisher<ClientEvent<OfferPayload>> OfferToClientPub;
    IPublisher<ClientEvent<AnswerPayload>> AnswerToClientPub;
    ISubscriber<ClientEvent<AddIceCandidatePayload>> AddIceCandidateToClientSub;
    ISubscriber<ClientEvent<OfferPayload>> OfferToClientSub;
    ISubscriber<ClientEvent<AnswerPayload>> AnswerToClientSub;

    private Guid ServerId => ClientConnectionManagerService.ServerId;

    public async ValueTask AddIceCandidateAsync(Guid source, Guid target, string candidate)
    {
        if (target == ClientConnectionManagerService.ServerId)
        {
            WebViewService.SendMessage(new SignalConnectionEvent<AddIceCandidatePayload>(
                source,
                new AddIceCandidatePayload(candidate)
            ));
        }
        else
        {
            AddIceCandidateToClientPub.Publish(new ClientEvent<AddIceCandidatePayload>(
                target,
                new AddIceCandidatePayload(candidate)
            ));
        }
    }

    public async ValueTask OfferAsync(Guid source, Guid target, string sdp)
    {
        if (target == ClientConnectionManagerService.ServerId)
        {
            WebViewService.SendMessage(new SignalConnectionEvent<OfferPayload>(
                source,
                new OfferPayload(sdp)
            ));
        }
        else
        {
            OfferToClientPub.Publish(new ClientEvent<OfferPayload>(
                target,
                new OfferPayload(sdp)
            ));
        }
    }

    public async ValueTask AnswerAsync(Guid source, Guid target, string sdp)
    {
        if (target == ClientConnectionManagerService.ServerId)
        {
            WebViewService.SendMessage(new SignalConnectionEvent<AnswerPayload>(
                source,
                new AnswerPayload(sdp)
            ));
        }
        else
        {
            AnswerToClientPub.Publish(new ClientEvent<AnswerPayload>(
                target,
                new AnswerPayload(sdp)
            ));
        }
    }

    public IDisposable CreateConnection(Guid source, Guid target)
    {
        return Disposable.Empty;
    }

    public IDisposable SubscribeAddIceCandidateAwait(Guid source, Guid target, Func<string?, CancellationToken, ValueTask> handler)
    {
        throw new NotImplementedException();
    }

    public IDisposable SubscribeAnswerAwait(Guid source, Guid target, Func<string, CancellationToken, ValueTask> handler)
    {
        throw new NotImplementedException();
    }

    public IDisposable SubscribeOfferAwait(Guid source, Guid target, Func<string, CancellationToken, ValueTask> handler)
    {
        throw new NotImplementedException();
    }
}

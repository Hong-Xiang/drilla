using DualDrill.Engine.Connection;
using MessagePipe;
using Microsoft.AspNetCore.SignalR;
using System.Reactive.Disposables;

namespace DualDrill.Server.Connection;

sealed class SignalConnectionOverSignalRProvider(
   ClientConnectionManagerService ClientStore,
   IAsyncPublisher<PairIdentity, OfferPayload> OfferPublisher,
   IAsyncPublisher<PairIdentity, AnswerPayload> AnswerPublisher,
   IAsyncPublisher<PairIdentity, AddIceCandidatePayload> AddIceCandidatePublisher,
   IAsyncSubscriber<PairIdentity, OfferPayload> OfferSubscriber,
   IAsyncSubscriber<PairIdentity, AnswerPayload> AnswerSubscriber,
   IAsyncSubscriber<PairIdentity, AddIceCandidatePayload> AddIceCandidateSubscriber,
   IHubContext<DrillHub, IDrillHubClient> HubContext
) : ISignalConnectionProviderService
{
    public async ValueTask AddIceCandidateAsync(Guid source, Guid target, string candidate)
    {
        await AddIceCandidatePublisher.PublishAsync(new PairIdentity(source, target), new(candidate));
    }

    public IDisposable CreateConnection(Guid source, Guid target)
    {
        return CreateSignalRPushForConnection(source, target);
    }

    private CompositeDisposable CreateSignalRPushForConnection(Guid source, Guid target)
    {
        var disposables = new CompositeDisposable();

        IDrillHubClient GetClient()
        {
            var connectionId = ClientStore.GetConnectionId(target)
                               ?? throw new InvalidOperationException($"ConnectionId for {target} not set yet");
            return HubContext.Clients.Clients(connectionId);
        }

        disposables.Add(
            OfferSubscriber.Subscribe(
                new(source, target),
                async (payload, cancellation) =>
                {
                    await GetClient().Offer(source, payload.Sdp);
                }
        ));

        disposables.Add(
            AnswerSubscriber.Subscribe(
                new(source, target),
                async (payload, cancellation) =>
                {
                    await GetClient().Answer(source, payload.Sdp);
                }
        ));

        disposables.Add(
            AddIceCandidateSubscriber.Subscribe(
                new(source, target),
                async (payload, cancellation) =>
                {
                    await GetClient().AddIceCandidate(source, payload.Candidate);
                }
        ));
        return disposables;
    }


    public async ValueTask AnswerAsync(Guid source, Guid target, string sdp)
    {
        await AnswerPublisher.PublishAsync(new PairIdentity(source, target), new(sdp));
    }

    public async ValueTask OfferAsync(Guid source, Guid target, string sdp)
    {
        await OfferPublisher.PublishAsync(new PairIdentity(source, target), new(sdp));
    }

    public IDisposable SubscribeAddIceCandidateAwait(Guid source, Guid target, Func<string?, CancellationToken, ValueTask> handler)
    {
        return AddIceCandidateSubscriber.Subscribe(new PairIdentity(source, target), async (candidate, cancellation) =>
        {
            await handler(candidate.Candidate, cancellation);
        });
    }

    public IDisposable SubscribeAnswerAwait(Guid source, Guid target, Func<string, CancellationToken, ValueTask> handler)
    {
        return AnswerSubscriber.Subscribe(new PairIdentity(source, target), async (answer, cancellation) =>
        {
            await handler(answer.Sdp, cancellation);
        });
    }

    public IDisposable SubscribeOfferAwait(Guid source, Guid target, Func<string, CancellationToken, ValueTask> handler)
    {
        return OfferSubscriber.Subscribe(new PairIdentity(source, target), async (offer, cancellation) =>
        {
            await handler(offer.Sdp, cancellation);
        });
    }
}

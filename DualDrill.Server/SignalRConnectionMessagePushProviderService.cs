using DualDrill.Engine.Connection;
using MessagePipe;
using Microsoft.AspNetCore.SignalR;
using R3;

namespace DualDrill.Server;

sealed partial class SignalRConnectionMessagePushProviderService(
    ILogger<SignalRConnectionMessagePushProviderService> Logger,
    ClientStore ClientStore,
    IAsyncSubscriber<PairIdentity, OfferPayload> OfferSubscriber,
    IAsyncSubscriber<PairIdentity, AnswerPayload> AnswerSubscriber,
    IAsyncSubscriber<PairIdentity, AddIceCandidatePayload> AddIceCandidateSubscriber,
    IHubContext<DrillHub, IDrillHubClient> HubContext
) : ISignalConnectionOverSignalRService
{
    public IDisposable CreateConnection(Guid source, Guid target)
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
}


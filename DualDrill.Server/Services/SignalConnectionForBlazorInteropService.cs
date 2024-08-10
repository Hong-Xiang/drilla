using DualDrill.Engine.Connection;
using MessagePipe;
using Microsoft.JSInterop;

namespace DualDrill.Server.Services;

public sealed class SignalConnectionForBlazorInteropService(
    IClient Client,
    IAsyncPublisher<PairIdentity, OfferPayload> Offers,
    IAsyncPublisher<PairIdentity, AnswerPayload> Answers,
    IAsyncPublisher<PairIdentity, AddIceCandidatePayload> Candidates)
{
    private readonly PairIdentity SendId = new(Client.Id, Guid.Empty);

    [JSInvokable]
    public async ValueTask AddIceCandidateAsync(string? candidate, CancellationToken cancellation)
    {
        await Candidates.PublishAsync(SendId, new(candidate), cancellation);
    }

    [JSInvokable]
    public async ValueTask AnswerAsync(string answer, CancellationToken cancellation)
    {
        await Answers.PublishAsync(SendId, new(answer), cancellation);
    }

    [JSInvokable]
    public async ValueTask OfferAsync(string offer, CancellationToken cancellation)
    {
        await Offers.PublishAsync(SendId, new(offer), cancellation);
    }
}

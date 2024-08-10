using MessagePipe;
using System.Reflection.Metadata;

namespace DualDrill.Engine.Connection;

public enum SignalConnectionProvider
{
    None,
    SignalR,
    BlazorServerInteractive,
    WebView2Message,
    HTTPPooling
}

public interface ISignalConnectionOverSignalRService
{
    IDisposable CreateConnection(Guid source, Guid target);
}

public interface ISignalConnectionProviderService
{
    IDisposable CreateConnection(Guid source, Guid target, SignalConnectionProvider provider);
    ValueTask OfferAsync(Guid source, Guid target, string sdp);
    ValueTask AnswerAsync(Guid source, Guid target, string sdp);
    ValueTask AddIceCandidateAsync(Guid source, Guid target, string candidate);

    IDisposable SubscribeOfferAwait(Guid source, Guid target, Func<string, CancellationToken, ValueTask> handler);
    IDisposable SubscribeAnswerAwait(Guid source, Guid target, Func<string, CancellationToken, ValueTask> handler);
    IDisposable SubscribeAddIceCandidateAwait(Guid source, Guid target, Func<string?, CancellationToken, ValueTask> handler);
}

public sealed class MessagePipeSignalConnectionProvider(
    IAsyncPublisher<PairIdentity, OfferPayload> OfferPublisher,
    IAsyncPublisher<PairIdentity, AnswerPayload> AnswerPublisher,
    IAsyncPublisher<PairIdentity, AddIceCandidatePayload> AddIceCandidatePublisher,
    IAsyncSubscriber<PairIdentity, OfferPayload> OfferSubscriber,
    IAsyncSubscriber<PairIdentity, AnswerPayload> AnswerSubscriber,
    IAsyncSubscriber<PairIdentity, AddIceCandidatePayload> AddIceCandidateSubscriber,
    ISignalConnectionOverSignalRService SignalRImplementationService
) : ISignalConnectionProviderService
{
    public async ValueTask AddIceCandidateAsync(Guid source, Guid target, string candidate)
    {
        await AddIceCandidatePublisher.PublishAsync(new PairIdentity(source, target), new(candidate));
    }

    public IDisposable CreateConnection(Guid source, Guid target, SignalConnectionProvider provider)
    {
        if (provider == SignalConnectionProvider.SignalR)
        {
            return SignalRImplementationService.CreateConnection(source, target);
        }
        throw new NotImplementedException($"privoder {Enum.GetName(provider)}");
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

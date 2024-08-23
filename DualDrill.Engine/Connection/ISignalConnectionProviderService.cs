using DualDrill.Engine.Event;

namespace DualDrill.Engine.Connection;

public interface ISignalConnectionProviderService
{
    ValueTask OfferAsync(ConnectionEvent<OfferEvent> e, CancellationToken cancellation);
    ValueTask AnswerAsync(ConnectionEvent<AnswerEvent> e, CancellationToken cancellation);
    ValueTask AddIceCandidateAsync(ConnectionEvent<AddIceCandidateEvent> e, CancellationToken cancellation);

    IDisposable SubscribeOfferAwait(Guid target, Func<ConnectionEvent<OfferEvent>, CancellationToken, ValueTask> handler);
    IDisposable SubscribeAnswerAwait(Guid target, Func<ConnectionEvent<AnswerEvent>, CancellationToken, ValueTask> handler);
    IDisposable SubscribeAddIceCandidateAwait(Guid target, Func<ConnectionEvent<AddIceCandidateEvent>, CancellationToken, ValueTask> handler);
}

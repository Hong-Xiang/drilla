namespace DualDrill.Engine.Connection;

public interface ISignalConnectionProviderService
{
    IDisposable CreateConnection(Guid source, Guid target);
    ValueTask OfferAsync(Guid source, Guid target, string sdp);
    ValueTask AnswerAsync(Guid source, Guid target, string sdp);
    ValueTask AddIceCandidateAsync(Guid source, Guid target, string candidate);

    IDisposable SubscribeOfferAwait(Guid source, Guid target, Func<string, CancellationToken, ValueTask> handler);
    IDisposable SubscribeAnswerAwait(Guid source, Guid target, Func<string, CancellationToken, ValueTask> handler);
    IDisposable SubscribeAddIceCandidateAwait(Guid source, Guid target, Func<string?, CancellationToken, ValueTask> handler);
}

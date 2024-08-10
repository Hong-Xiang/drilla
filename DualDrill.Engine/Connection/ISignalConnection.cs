using R3;
namespace DualDrill.Engine.Connection;

public interface ISignalConnection
{
    Guid SelfId { get; }
    Guid PeerId { get; }
    ValueTask OfferAsync(string offer, CancellationToken cancellation);
    Observable<string> OnOffer { get; }

    ValueTask AnswerAsync(string answer, CancellationToken cancellation);
    Observable<string> OnAnswer { get; }

    ValueTask AddIceCandidateAsync(string? candidate, CancellationToken cancellation);
    Observable<string?> OnAddIceCandidate { get; }
}

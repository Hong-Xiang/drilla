namespace DualDrill.Engine.Connection;

public readonly record struct OfferPayload(string Sdp)
{
}
public readonly record struct AnswerPayload(string Sdp)
{
}
public readonly record struct AddIceCandidatePayload(string? Candidate)
{
}

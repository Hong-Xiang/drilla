namespace DualDrill.Engine.Connection;

public readonly record struct ClientEvent<TPayload>(
    Guid SourceClientId,
    TPayload Payload
)
{
}

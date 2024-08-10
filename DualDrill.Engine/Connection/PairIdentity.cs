namespace DualDrill.Engine.Connection;

public readonly record struct PairIdentity(
    Guid SourceClientId,
    Guid TargetClientId
)
{
}

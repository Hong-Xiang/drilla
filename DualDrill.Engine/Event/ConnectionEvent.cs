namespace DualDrill.Engine.Event;

public sealed record class ConnectionEvent<T>(
    Guid SourceId,
    Guid TargetId,
    T Data
)
{
}

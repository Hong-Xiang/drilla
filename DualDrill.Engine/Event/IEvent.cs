namespace DualDrill.Engine.Event;

public interface IEvent
{
    public Guid SourceConnectionId { get; }
    public Guid TargetConnectionId { get; }
}


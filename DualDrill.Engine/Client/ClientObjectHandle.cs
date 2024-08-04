namespace DualDrill.Engine.Client;

public readonly record struct ClientObjectHandle(
    Guid ClientId,
    string TypeName,
    string Handle
)
{
}

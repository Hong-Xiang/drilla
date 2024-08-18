namespace DualDrill.Engine.Connection;

public interface IPeerConnectionProviderService
{
    public ValueTask<IPeerConnection> CreatePeerConnectionAsync(Guid clientId, CancellationToken cancellation);
}


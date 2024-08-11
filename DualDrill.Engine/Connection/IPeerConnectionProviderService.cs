using DualDrill.Engine.WebRTC;

namespace DualDrill.Engine.Connection;

public interface IPeerConnectionProviderService
{
    public IRTCPeerConnection CreatePeerConnection(Guid clientId);
}

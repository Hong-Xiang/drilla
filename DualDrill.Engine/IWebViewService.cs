using DualDrill.Engine.Connection;
using DualDrill.Engine.Headless;
using DualDrill.Engine.Media;

namespace DualDrill.Engine;

public interface IWebViewService
{
    ValueTask<IPeerConnection> GetPeerConnectionAsync(Guid clientId);
    ValueTask<IMediaStream> Capture(HeadlessSurface surface, int frameRate);
    ValueTask StartAsync(CancellationToken cancellation);
    ValueTask CreateCanvas2D();
}

public interface ICanvas2D
{
    ValueTask ShowStream(string id);
}

using DualDrill.Engine.Headless;
using DualDrill.Engine.Media;

namespace DualDrill.Engine;

public interface IWebViewSharedBuffer
{
    Guid Id { get; }
    Span<byte> Span { get; }
}
public interface IHostToWebViewEvent
{
    string MessageType { get; }
}
public interface IWebViewService
{
    ValueTask<IMediaStream> CaptureAsync(HeadlessSurface surface, int frameRate);
    ValueTask StartAsync(CancellationToken cancellation);
    ValueTask SendMessageAsync<T>(T data, CancellationToken cancellation) where T : notnull;
}

public interface IWebViewInteropService
{
    ValueTask<IWebViewSharedBuffer> CreateSurfaceSharedBufferAsync(HeadlessSurface surface, CancellationToken cancellation);

}

public interface ICanvas2D
{
    ValueTask ShowStream(string id);
}

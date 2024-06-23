using DualDrill.Engine.Renderer;
using DualDrill.Graphics;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace DualDrill.Engine;

public sealed class FrameService : IFrameService, IDisposable
{
    public ILogger<FrameService> Logger { get; }
    readonly TriangleRenderer Renderer;
    readonly GPUDevice Device;
    public FrameService(
        ILogger<FrameService> logger,
        GPUDevice device,
        TriangleRenderer renderer)
    {
        Logger = logger;
        Device = device;
        Renderer = renderer;
    }
    public async ValueTask OnFrameAsync(FrameContext frameContext, CancellationToken cancellation)
    {
        using var queue = Device.GetQueue();
        var texture = frameContext.Surface.GetCurrentTexture();
        if (texture is not null)
        {
            await Renderer.RenderAsync(frameContext.FrameIndex, queue, texture);
        }
    }

    public void Dispose()
    {
    }
}

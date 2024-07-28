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
    readonly SimpleColorRenderer Renderer;
    readonly RotateCubeRenderer CubeRenderer;
    readonly GPUDevice Device;
    public FrameService(
        ILogger<FrameService> logger,
        GPUDevice device,
        SimpleColorRenderer renderer,
        RotateCubeRenderer cubeRenderer
        )
    {
        Logger = logger;
        Device = device;
        Renderer = renderer;
        CubeRenderer = cubeRenderer;
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

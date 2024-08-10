using DualDrill.Engine.Renderer;
using DualDrill.Graphics;
using Microsoft.Extensions.Logging;

namespace DualDrill.Engine;

public sealed class FrameService : IFrameService, IDisposable
{
    public ILogger<FrameService> Logger { get; }
    public FrameSimulationService Simulation { get; }

    readonly SimpleColorRenderer Renderer;
    readonly RotateCubeRenderer CubeRenderer;
    readonly GPUDevice Device;
    public FrameService(
        ILogger<FrameService> logger,
        GPUDevice device,
        SimpleColorRenderer renderer,
        RotateCubeRenderer cubeRenderer,
        FrameSimulationService simulation)
    {
        Logger = logger;
        Device = device;
        Renderer = renderer;
        CubeRenderer = cubeRenderer;
        Simulation = simulation;
    }
    public async ValueTask<FrameContext> OnFrameAsync(FrameContext frameContext, CancellationToken cancellation)
    {
        var texture = frameContext.Surface.GetCurrentTexture();
        var mvp = Simulation.CubeSimulation(frameContext, out var result);
        if (texture is not null)
        {
            using var queue = Device.GetQueue();
            await CubeRenderer.RenderAsync(frameContext.FrameIndex, queue, texture, mvp);
        }
        return result;
    }

    public void Dispose()
    {
    }
}

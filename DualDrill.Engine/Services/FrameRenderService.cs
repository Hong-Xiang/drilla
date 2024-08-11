using DualDrill.Engine.Renderer;
using DualDrill.Engine.Scene;
using DualDrill.Graphics;
using Microsoft.Extensions.Logging;

namespace DualDrill.Engine.Services;

public sealed class FrameRenderService(
    ILogger<FrameRenderService> Logger,
    GPUDevice Device,
    WebGPULogoRenderer LogoRenderer,
    RotateCubeRenderer CubeRenderer,
    FrameSimulationService Simulation,
    ClearColorRenderer ClearColorRenderer) : IFrameRenderService
{
    public async ValueTask RenderAsync(long frame, RenderScene scene, GPUTexture renderTarget, CancellationToken cancellation)
    {
        using var queue = Device.GetQueue();
        ClearColorRenderer.Render(frame, queue, renderTarget, scene.ClearColor);
        LogoRenderer.Render(frame, queue, renderTarget, scene.LogoState);
        CubeRenderer.Render(frame, queue, renderTarget, new(scene.Camera, scene.Cube));
    }
}

using DualDrill.Engine.Renderer;
using DualDrill.Engine.Scene;
using DualDrill.Graphics;
using Microsoft.Extensions.Logging;

namespace DualDrill.Engine.Services;

public sealed record class FrameRenderService(
    ILogger<FrameRenderService> Logger,
    GPUDevice Device,
    WebGPULogoRenderer LogoRenderer,
    RotateCubeRenderer CubeRenderer,
    ClearColorRenderer ClearColorRenderer
    //VolumeRenderer VolumeRenderer
    ) : IFrameRenderService
{
    public async ValueTask RenderAsync(long frame, RenderScene scene, GPUTexture renderTarget, CancellationToken cancellation)
    {
        using var queue = Device.GetQueue();
        ClearColorRenderer.Render(frame, queue, renderTarget, scene.ClearColor);
        //VolumeRenderer.Render(frame, queue, renderTarget, new()
        //{
        //    Theta = MathF.PI / 2.0f,
        //    Phi = 0,
        //    Z = 0,
        //    Window = 0.1f
        //});
        LogoRenderer.Render(frame, queue, renderTarget, scene.LogoState);
        CubeRenderer.Render(frame, queue, renderTarget, new(scene.Camera, scene.Cube));
    }
}

using DualDrill.Engine.Renderer;
using DualDrill.Engine.Scene;
using DualDrill.Graphics;
using DualDrill.Graphics.Backend;
using Microsoft.Extensions.Logging;

namespace DualDrill.Engine.Services;

public sealed record class FrameRenderService(
    ILogger<FrameRenderService> Logger,
    IGPUDevice Device,
    WebGPULogoRenderer LogoRenderer,
    RotateCubeRenderer CubeRenderer,
    ClearColorRenderer ClearColorRenderer,
    StaticTriangleRenderer StaticTriangleRenderer
    //VolumeRenderer VolumeRenderer
    ) : IFrameRenderService
{
    public async ValueTask RenderAsync(long frame, RenderScene scene, IGPUTexture renderTarget, CancellationToken cancellation)
    {
        var queue = Device.Queue;
        ClearColorRenderer.Render(frame, queue, renderTarget, scene.ClearColor);
        //VolumeRenderer.Render(frame, queue, renderTarget, new()
        //{
        //    Theta = MathF.PI / 2.0f,
        //    Phi = 0,
        //    Z = 0,
        //    Window = 0.1f
        //});
        StaticTriangleRenderer.Render(frame, queue, renderTarget, new());
        //LogoRenderer.Render(frame, queue, renderTarget, scene.LogoState);
        //CubeRenderer.Render(frame, queue, renderTarget, new(scene.Camera, scene.Cube));
    }
}

public sealed record class FrameRenderService2(
    ILogger<FrameRenderService2> Logger,
    IGPUDevice Device2,
    WebGPULogoRenderer LogoRenderer,
    RotateCubeRenderer CubeRenderer,
    ClearColorRenderer ClearColorRenderer,
    StaticTriangleRenderer StaticTriangleRenderer
//VolumeRenderer VolumeRenderer
)
{

}

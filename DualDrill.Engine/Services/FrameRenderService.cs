using DualDrill.Engine.Renderer;
using DualDrill.Engine.Scene;
using DualDrill.Graphics;

namespace DualDrill.Engine.Services;

public sealed record class FrameRenderService(
    IGPUDevice Device,
    WebGPULogoRenderer LogoRenderer,
    RotateCubeRenderer CubeRenderer,
    ClearColorRenderer ClearColorRenderer,
    StaticTriangleRenderer StaticTriangleRenderer
    ) : IFrameRenderService
{
    public ValueTask RenderAsync(long frame, RenderScene scene, IGPUTexture renderTarget, CancellationToken cancellation)
    {
        var queue = Device.Queue;
        ClearColorRenderer.Render(frame, queue, renderTarget, scene.ClearColor);
        StaticTriangleRenderer.Render(frame, queue, renderTarget, new());
        LogoRenderer.Render(frame, queue, renderTarget, scene.LogoState);
        CubeRenderer.Render(frame, queue, renderTarget, new(scene.Camera, scene.Cube));
        return ValueTask.CompletedTask;
    }
}

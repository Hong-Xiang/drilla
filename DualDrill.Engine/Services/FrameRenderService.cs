using DualDrill.Engine.Renderer;
using DualDrill.Engine.Scene;
using DualDrill.Graphics;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Numerics;

namespace DualDrill.Engine.Services;

public sealed class FrameRenderService : IFrameRenderService, IDisposable
{
    public ILogger<FrameRenderService> Logger { get; }
    public FrameSimulationService Simulation { get; }

    readonly SimpleColorRenderer Renderer;
    readonly RotateCubeRenderer CubeRenderer;
    readonly GPUDevice Device;
    public FrameRenderService(
        ILogger<FrameRenderService> logger,
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
    public async ValueTask RenderAsync(long frame, RenderScene scene, GPUTexture renderTarget, CancellationToken cancellation)
    {
        var mvp = scene.Cube.ModelMatrix * scene.Camera.ViewProjectionMatrix;
        using var queue = Device.GetQueue();
        Span<float> buffer = stackalloc float[16];
        CopyToBuffer(mvp, buffer);
        CubeRenderer.Render(frame, queue, renderTarget, buffer);
    }
    private unsafe void CopyToBuffer(Matrix4x4 m, Span<float> target)
    {
        Debug.Assert(target.Length == 16);
        var sourceBuffer = new Span<float>(&m, 16);
        sourceBuffer.CopyTo(target);
    }
    public void Dispose()
    {
    }
}

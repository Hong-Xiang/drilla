using DualDrill.Graphics;
using System.Numerics;

namespace DualDrill.Engine.Renderer;

public sealed class ClearColorRenderer : IRenderer<Vector3>
{
    public ClearColorRenderer(GPUDevice device)
    {
        Device = device;
    }

    public GPUDevice Device { get; }

    public void Render(double time, GPUQueue queue, GPUTexture texture, Vector3 data)
    {
        using var view = texture.CreateView();
        using var encoder = Device.CreateCommandEncoder(new());
        using var rp = encoder.BeginRenderPass(new()
        {
            ColorAttachments = (GPURenderPassColorAttachment[])[
                new GPURenderPassColorAttachment() {
                    View = view,
                    LoadOp = GPULoadOp.Clear,
                    StoreOp = GPUStoreOp.Store,
                    ClearValue = new GPUColor {
                        R = data.X,
                        G = data.Y,
                        B = data.Z,
                        A = 1.0f
                    }
                }
            ]
        });
        rp.End();
        using var drawCommands = encoder.Finish(new());
        queue.Submit([drawCommands]);
    }
}

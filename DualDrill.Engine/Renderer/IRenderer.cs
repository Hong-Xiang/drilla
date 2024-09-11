using DualDrill.Graphics;

namespace DualDrill.Engine.Renderer;

public interface IRenderer<T>
//where T : allows ref struct
{
    void Render(double time, IGPUQueue queue, IGPUTexture texture, T data);
}

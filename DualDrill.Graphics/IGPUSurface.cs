
namespace DualDrill.Graphics;
public interface IGPUSurface
{
    GPUTexture? GetCurrentTexture();
    void Configure(GPUSurfaceConfiguration configuration);
    void Unconfigure();
    void Present();
}

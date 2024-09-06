
namespace DualDrill.Graphics;
public partial interface IGPUSurface
{
    GPUTexture? GetCurrentTexture();
    void Configure(GPUSurfaceConfiguration configuration);
    void Unconfigure();
    void Present();
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics;

public interface IGPUSurface
{
    GPUTexture? GetCurrentTexture();
    void Configure(GPUSurfaceConfiguration configuration);
    void Unconfigure();
}


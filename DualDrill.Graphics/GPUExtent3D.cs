using DualDrill.Graphics.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics;

public partial struct GPUExtent3D
{
    public static implicit operator WGPUExtent3D(GPUExtent3D value)
    {
        return new WGPUExtent3D
        {
            width = (uint)value.Width,
            height = (uint)value.Height,
            depthOrArrayLayers = (uint)value.DepthOrArrayLayers,
        };
    }
}

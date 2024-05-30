using DualDrill.Graphics.Native;
using DualDrill.Graphics.WebGPU.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics;

public sealed class GPUSurface
{
    internal NativeHandle<WGPUDisposer, WGPUSurfaceImpl> Handle { get; }
}

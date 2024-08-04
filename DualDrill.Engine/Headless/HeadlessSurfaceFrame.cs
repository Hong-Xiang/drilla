using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DualDrill.Common.Abstraction.Signal;
using DualDrill.Graphics;

namespace DualDrill.Engine.Headless;

public readonly record struct HeadlessSurfaceFrame(
    GPUExtent3D Size,
    GPUTextureFormat Format,
    ReadOnlyMemory<byte> Data)
{
}

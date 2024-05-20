using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics;

public unsafe sealed class ShaderModule(
    Silk.NET.WebGPU.WebGPU Api,
    Silk.NET.WebGPU.ShaderModule* Handle
)
{
    public Silk.NET.WebGPU.ShaderModule* Handle { get; } = Handle;

}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics;

[StructLayout(LayoutKind.Sequential)]
public readonly record struct GPUBool(uint Value)
{
    public static implicit operator GPUBool(bool value) => value ? new(1) : new(0);
    public static implicit operator uint(GPUBool value) => value.Value;
}

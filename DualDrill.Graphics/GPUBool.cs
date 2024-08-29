using System.Runtime.InteropServices;

namespace DualDrill.Graphics;

[StructLayout(LayoutKind.Sequential)]
public readonly record struct GPUBool(uint Value)
{
    public static implicit operator GPUBool(bool value) => value ? new(1) : new(0);
    public static implicit operator uint(GPUBool value) => value.Value;
}

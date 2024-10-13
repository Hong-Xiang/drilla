using System.Runtime.InteropServices;

namespace DualDrill.Mathematics;


[StructLayout(LayoutKind.Sequential)]
public partial struct vec2i16 {
	public short x;
	public short y;
}

[StructLayout(LayoutKind.Sequential)]
public partial struct vec2u16 {
	public ushort x;
	public ushort y;
}

[StructLayout(LayoutKind.Sequential)]
public partial struct vec2f16 {
	public Half x;
	public Half y;
}


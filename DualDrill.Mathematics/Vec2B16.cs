





using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using static DualDrill.Mathematics.DMath;

namespace DualDrill.Mathematics;





[StructLayout(LayoutKind.Sequential)]
public partial struct vec2i16 {
	public short x;
	public short y;




	public vec3i16 xxx {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec3(x, x, x);
	}

	public vec3i16 xxy {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec3(x, x, y);
	}



	public vec3i16 xyx {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec3(x, y, x);
	}

	public vec3i16 xyy {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec3(x, y, y);
	}





	public vec3i16 yxx {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec3(y, x, x);
	}

	public vec3i16 yxy {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec3(y, x, y);
	}



	public vec3i16 yyx {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec3(y, y, x);
	}

	public vec3i16 yyy {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec3(y, y, y);
	}







	public vec4i16 xxxx {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec4(x, x, x, x);
	}

	public vec4i16 xxxy {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec4(x, x, x, y);
	}



	public vec4i16 xxyx {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec4(x, x, y, x);
	}

	public vec4i16 xxyy {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec4(x, x, y, y);
	}





	public vec4i16 xyxx {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec4(x, y, x, x);
	}

	public vec4i16 xyxy {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec4(x, y, x, y);
	}



	public vec4i16 xyyx {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec4(x, y, y, x);
	}

	public vec4i16 xyyy {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec4(x, y, y, y);
	}







	public vec4i16 yxxx {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec4(y, x, x, x);
	}

	public vec4i16 yxxy {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec4(y, x, x, y);
	}



	public vec4i16 yxyx {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec4(y, x, y, x);
	}

	public vec4i16 yxyy {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec4(y, x, y, y);
	}





	public vec4i16 yyxx {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec4(y, y, x, x);
	}

	public vec4i16 yyxy {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec4(y, y, x, y);
	}



	public vec4i16 yyyx {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec4(y, y, y, x);
	}

	public vec4i16 yyyy {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec4(y, y, y, y);
	}






}

public partial class DMath {
	public static vec2i16 vec2(short e) {
		return new vec2i16() {
			x = e,
			y = e
		};
	}
	public static vec2i16 vec2(short x, short y) {
		return new vec2i16() {
			x = x,
			y = y
		};
	}
}

[StructLayout(LayoutKind.Sequential)]
public partial struct vec2u16 {
	public ushort x;
	public ushort y;




	public vec3u16 xxx {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec3(x, x, x);
	}

	public vec3u16 xxy {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec3(x, x, y);
	}



	public vec3u16 xyx {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec3(x, y, x);
	}

	public vec3u16 xyy {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec3(x, y, y);
	}





	public vec3u16 yxx {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec3(y, x, x);
	}

	public vec3u16 yxy {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec3(y, x, y);
	}



	public vec3u16 yyx {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec3(y, y, x);
	}

	public vec3u16 yyy {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec3(y, y, y);
	}







	public vec4u16 xxxx {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec4(x, x, x, x);
	}

	public vec4u16 xxxy {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec4(x, x, x, y);
	}



	public vec4u16 xxyx {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec4(x, x, y, x);
	}

	public vec4u16 xxyy {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec4(x, x, y, y);
	}





	public vec4u16 xyxx {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec4(x, y, x, x);
	}

	public vec4u16 xyxy {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec4(x, y, x, y);
	}



	public vec4u16 xyyx {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec4(x, y, y, x);
	}

	public vec4u16 xyyy {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec4(x, y, y, y);
	}







	public vec4u16 yxxx {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec4(y, x, x, x);
	}

	public vec4u16 yxxy {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec4(y, x, x, y);
	}



	public vec4u16 yxyx {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec4(y, x, y, x);
	}

	public vec4u16 yxyy {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec4(y, x, y, y);
	}





	public vec4u16 yyxx {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec4(y, y, x, x);
	}

	public vec4u16 yyxy {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec4(y, y, x, y);
	}



	public vec4u16 yyyx {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec4(y, y, y, x);
	}

	public vec4u16 yyyy {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => vec4(y, y, y, y);
	}






}

public partial class DMath {
	public static vec2u16 vec2(ushort e) {
		return new vec2u16() {
			x = e,
			y = e
		};
	}
	public static vec2u16 vec2(ushort x, ushort y) {
		return new vec2u16() {
			x = x,
			y = y
		};
	}
}




//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.InteropServices;
namespace DualDrill.Mathematics;
using static DMath;

[StructLayout(LayoutKind.Sequential)]
public partial struct vec2b{
    public System.Boolean x { get; set; }
    public System.Boolean y { get; set; }
    public vec2b xx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec2(x, x);
    }
    
    public vec2b yx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec2(y, x);
    }
    
    public vec2b xy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec2(x, y);
    }
    
    public vec2b yy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec2(y, y);
    }
    
    public vec3b xxx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec3(x, x, x);
    }
    
    public vec3b yxx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec3(y, x, x);
    }
    
    public vec3b xyx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec3(x, y, x);
    }
    
    public vec3b yyx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec3(y, y, x);
    }
    
    public vec3b xxy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec3(x, x, y);
    }
    
    public vec3b yxy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec3(y, x, y);
    }
    
    public vec3b xyy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec3(x, y, y);
    }
    
    public vec3b yyy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec3(y, y, y);
    }
    
    public vec4b xxxx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec4(x, x, x, x);
    }
    
    public vec4b yxxx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec4(y, x, x, x);
    }
    
    public vec4b xyxx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec4(x, y, x, x);
    }
    
    public vec4b yyxx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec4(y, y, x, x);
    }
    
    public vec4b xxyx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec4(x, x, y, x);
    }
    
    public vec4b yxyx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec4(y, x, y, x);
    }
    
    public vec4b xyyx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec4(x, y, y, x);
    }
    
    public vec4b yyyx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec4(y, y, y, x);
    }
    
    public vec4b xxxy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec4(x, x, x, y);
    }
    
    public vec4b yxxy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec4(y, x, x, y);
    }
    
    public vec4b xyxy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec4(x, y, x, y);
    }
    
    public vec4b yyxy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec4(y, y, x, y);
    }
    
    public vec4b xxyy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec4(x, x, y, y);
    }
    
    public vec4b yxyy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec4(y, x, y, y);
    }
    
    public vec4b xyyy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec4(x, y, y, y);
    }
    
    public vec4b yyyy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec4(y, y, y, y);
    }
    
}

public static partial class DMath{
    public static vec2b vec2(System.Boolean x, System.Boolean y){
        return new vec2b () {
            x = x,
            y = y
        };
    }
    
    public static vec2b vec2(System.Boolean e) => vec2(e, e);
}

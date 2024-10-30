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
public partial struct vec2f16{
    public System.Half x { get; set; }
    public System.Half y { get; set; }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2f16 operator -(vec2f16 v)
    {
        return vec2((System.Half)(-v.x), (System.Half)(-v.y));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2f16 operator +(vec2f16 left, vec2f16 right)
    {
        return vec2((System.Half)(left.x + right.x), (System.Half)(left.y + right.y));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2f16 operator -(vec2f16 left, vec2f16 right)
    {
        return vec2((System.Half)(left.x - right.x), (System.Half)(left.y - right.y));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2f16 operator *(vec2f16 left, vec2f16 right)
    {
        return vec2((System.Half)(left.x * right.x), (System.Half)(left.y * right.y));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2f16 operator /(vec2f16 left, vec2f16 right)
    {
        return vec2((System.Half)(left.x / right.x), (System.Half)(left.y / right.y));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2f16 operator %(vec2f16 left, vec2f16 right)
    {
        return vec2((System.Half)(left.x % right.x), (System.Half)(left.y % right.y));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2f16 operator +(System.Half left, vec2f16 right)
    {
        return vec2((System.Half)(left + right.x), (System.Half)(left + right.y));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2f16 operator +(vec2f16 left, System.Half right)
    {
        return vec2((System.Half)(left.x + right), (System.Half)(left.y + right));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2f16 operator -(System.Half left, vec2f16 right)
    {
        return vec2((System.Half)(left - right.x), (System.Half)(left - right.y));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2f16 operator -(vec2f16 left, System.Half right)
    {
        return vec2((System.Half)(left.x - right), (System.Half)(left.y - right));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2f16 operator *(System.Half left, vec2f16 right)
    {
        return vec2((System.Half)(left * right.x), (System.Half)(left * right.y));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2f16 operator *(vec2f16 left, System.Half right)
    {
        return vec2((System.Half)(left.x * right), (System.Half)(left.y * right));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2f16 operator /(System.Half left, vec2f16 right)
    {
        return vec2((System.Half)(left / right.x), (System.Half)(left / right.y));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2f16 operator /(vec2f16 left, System.Half right)
    {
        return vec2((System.Half)(left.x / right), (System.Half)(left.y / right));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2f16 operator %(System.Half left, vec2f16 right)
    {
        return vec2((System.Half)(left % right.x), (System.Half)(left % right.y));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2f16 operator %(vec2f16 left, System.Half right)
    {
        return vec2((System.Half)(left.x % right), (System.Half)(left.y % right));
    }
    
    public vec2f16 xx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec2(x, x);
    }
    
    public vec2f16 yx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec2(y, x);
    }
    
    public vec2f16 xy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec2(x, y);
    }
    
    public vec2f16 yy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec2(y, y);
    }
    
    public vec3f16 xxx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec3(x, x, x);
    }
    
    public vec3f16 yxx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec3(y, x, x);
    }
    
    public vec3f16 xyx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec3(x, y, x);
    }
    
    public vec3f16 yyx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec3(y, y, x);
    }
    
    public vec3f16 xxy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec3(x, x, y);
    }
    
    public vec3f16 yxy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec3(y, x, y);
    }
    
    public vec3f16 xyy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec3(x, y, y);
    }
    
    public vec3f16 yyy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec3(y, y, y);
    }
    
    public vec4f16 xxxx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec4(x, x, x, x);
    }
    
    public vec4f16 yxxx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec4(y, x, x, x);
    }
    
    public vec4f16 xyxx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec4(x, y, x, x);
    }
    
    public vec4f16 yyxx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec4(y, y, x, x);
    }
    
    public vec4f16 xxyx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec4(x, x, y, x);
    }
    
    public vec4f16 yxyx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec4(y, x, y, x);
    }
    
    public vec4f16 xyyx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec4(x, y, y, x);
    }
    
    public vec4f16 yyyx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec4(y, y, y, x);
    }
    
    public vec4f16 xxxy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec4(x, x, x, y);
    }
    
    public vec4f16 yxxy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec4(y, x, x, y);
    }
    
    public vec4f16 xyxy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec4(x, y, x, y);
    }
    
    public vec4f16 yyxy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec4(y, y, x, y);
    }
    
    public vec4f16 xxyy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec4(x, x, y, y);
    }
    
    public vec4f16 yxyy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec4(y, x, y, y);
    }
    
    public vec4f16 xyyy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec4(x, y, y, y);
    }
    
    public vec4f16 yyyy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => vec4(y, y, y, y);
    }
    
}

public static partial class DMath{
    public static vec2f16 vec2(System.Half x, System.Half y){
        return new vec2f16 () {
            x = x,
            y = y
        };
    }
    
    public static vec2f16 vec2(System.Half e) => vec2(e, e);
}

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
using DualDrill.CLSL.Language.ShaderAttribute;
namespace DualDrill.Mathematics;
using static DMath;

[StructLayout(LayoutKind.Sequential)]
public partial struct vec2i16{
    public Int16 x {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        set;
    
    }
    public Int16 y {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        set;
    
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2i16 operator -(vec2i16 v)
    {
        return vec2((Int16)(-v.x), (Int16)(-v.y));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2i16 operator +(vec2i16 left, vec2i16 right)
    {
        return vec2((Int16)(left.x + right.x), (Int16)(left.y + right.y));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2i16 operator -(vec2i16 left, vec2i16 right)
    {
        return vec2((Int16)(left.x - right.x), (Int16)(left.y - right.y));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2i16 operator *(vec2i16 left, vec2i16 right)
    {
        return vec2((Int16)(left.x * right.x), (Int16)(left.y * right.y));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2i16 operator /(vec2i16 left, vec2i16 right)
    {
        return vec2((Int16)(left.x / right.x), (Int16)(left.y / right.y));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2i16 operator %(vec2i16 left, vec2i16 right)
    {
        return vec2((Int16)(left.x % right.x), (Int16)(left.y % right.y));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2i16 operator +(Int16 left, vec2i16 right)
    {
        return vec2((Int16)(left + right.x), (Int16)(left + right.y));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2i16 operator +(vec2i16 left, Int16 right)
    {
        return vec2((Int16)(left.x + right), (Int16)(left.y + right));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2i16 operator -(Int16 left, vec2i16 right)
    {
        return vec2((Int16)(left - right.x), (Int16)(left - right.y));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2i16 operator -(vec2i16 left, Int16 right)
    {
        return vec2((Int16)(left.x - right), (Int16)(left.y - right));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2i16 operator *(Int16 left, vec2i16 right)
    {
        return vec2((Int16)(left * right.x), (Int16)(left * right.y));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2i16 operator *(vec2i16 left, Int16 right)
    {
        return vec2((Int16)(left.x * right), (Int16)(left.y * right));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2i16 operator /(Int16 left, vec2i16 right)
    {
        return vec2((Int16)(left / right.x), (Int16)(left / right.y));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2i16 operator /(vec2i16 left, Int16 right)
    {
        return vec2((Int16)(left.x / right), (Int16)(left.y / right));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2i16 operator %(Int16 left, vec2i16 right)
    {
        return vec2((Int16)(left % right.x), (Int16)(left % right.y));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2i16 operator %(vec2i16 left, Int16 right)
    {
        return vec2((Int16)(left.x % right), (Int16)(left.y % right));
    }
    
    public vec2i16 xx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec2(x, x);
    }
    
    public vec2i16 yx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec2(y, x);
    }
    
    public vec2i16 xy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec2(x, y);
    }
    
    public vec2i16 yy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec2(y, y);
    }
    
    public vec3i16 xxx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec3(x, x, x);
    }
    
    public vec3i16 yxx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec3(y, x, x);
    }
    
    public vec3i16 xyx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec3(x, y, x);
    }
    
    public vec3i16 yyx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec3(y, y, x);
    }
    
    public vec3i16 xxy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec3(x, x, y);
    }
    
    public vec3i16 yxy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec3(y, x, y);
    }
    
    public vec3i16 xyy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec3(x, y, y);
    }
    
    public vec3i16 yyy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec3(y, y, y);
    }
    
    public vec4i16 xxxx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec4(x, x, x, x);
    }
    
    public vec4i16 yxxx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec4(y, x, x, x);
    }
    
    public vec4i16 xyxx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec4(x, y, x, x);
    }
    
    public vec4i16 yyxx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec4(y, y, x, x);
    }
    
    public vec4i16 xxyx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec4(x, x, y, x);
    }
    
    public vec4i16 yxyx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec4(y, x, y, x);
    }
    
    public vec4i16 xyyx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec4(x, y, y, x);
    }
    
    public vec4i16 yyyx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec4(y, y, y, x);
    }
    
    public vec4i16 xxxy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec4(x, x, x, y);
    }
    
    public vec4i16 yxxy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec4(y, x, x, y);
    }
    
    public vec4i16 xyxy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec4(x, y, x, y);
    }
    
    public vec4i16 yyxy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec4(y, y, x, y);
    }
    
    public vec4i16 xxyy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec4(x, x, y, y);
    }
    
    public vec4i16 yxyy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec4(y, x, y, y);
    }
    
    public vec4i16 xyyy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec4(x, y, y, y);
    }
    
    public vec4i16 yyyy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec4(y, y, y, y);
    }
    
}


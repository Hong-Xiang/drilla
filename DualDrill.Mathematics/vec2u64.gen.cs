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
public partial struct vec2u64{
    internal Vector128<UInt64> Data;
    
    public UInt64 x {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => Data[0];
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleSetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        set {
            Data = Vector128.Create(value, Data[1]);
        }
        
    }
    
    public UInt64 y {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => Data[1];
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleSetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        set {
            Data = Vector128.Create(Data[0], value);
        }
        
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2u64 operator -(vec2u64 v)
    {
        return new() { Data = - v.Data };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2u64 operator +(vec2u64 left, vec2u64 right)
    {
        return new() { Data = left.Data + right.Data };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2u64 operator -(vec2u64 left, vec2u64 right)
    {
        return new() { Data = left.Data - right.Data };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2u64 operator *(vec2u64 left, vec2u64 right)
    {
        return new() { Data = left.Data * right.Data };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2u64 operator /(vec2u64 left, vec2u64 right)
    {
        return new() { Data = left.Data / right.Data };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2u64 operator %(vec2u64 left, vec2u64 right)
    {
        return vec2((UInt64)(left.x % right.x), (UInt64)(left.y % right.y));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2u64 operator +(UInt64 left, vec2u64 right)
    {
        return new() { Data = Vector128.Create(left) + right.Data };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2u64 operator +(vec2u64 left, UInt64 right)
    {
        return new() { Data = left.Data + Vector128.Create(right) };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2u64 operator -(UInt64 left, vec2u64 right)
    {
        return new() { Data = Vector128.Create(left) - right.Data };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2u64 operator -(vec2u64 left, UInt64 right)
    {
        return new() { Data = left.Data - Vector128.Create(right) };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2u64 operator *(UInt64 left, vec2u64 right)
    {
        return new() { Data = Vector128.Create(left) * right.Data };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2u64 operator *(vec2u64 left, UInt64 right)
    {
        return new() { Data = left.Data * Vector128.Create(right) };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2u64 operator /(UInt64 left, vec2u64 right)
    {
        return new() { Data = Vector128.Create(left) / right.Data };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2u64 operator /(vec2u64 left, UInt64 right)
    {
        return new() { Data = left.Data / Vector128.Create(right) };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2u64 operator %(UInt64 left, vec2u64 right)
    {
        return vec2((UInt64)(left % right.x), (UInt64)(left % right.y));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2u64 operator %(vec2u64 left, UInt64 right)
    {
        return vec2((UInt64)(left.x % right), (UInt64)(left.y % right));
    }
    
    public vec2u64 xx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec2(x, x);
    }
    
    public vec2u64 yx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec2(y, x);
    }
    
    public vec2u64 xy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec2(x, y);
    }
    
    public vec2u64 yy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec2(y, y);
    }
    
    public vec3u64 xxx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec3(x, x, x);
    }
    
    public vec3u64 yxx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec3(y, x, x);
    }
    
    public vec3u64 xyx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec3(x, y, x);
    }
    
    public vec3u64 yyx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec3(y, y, x);
    }
    
    public vec3u64 xxy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec3(x, x, y);
    }
    
    public vec3u64 yxy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec3(y, x, y);
    }
    
    public vec3u64 xyy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec3(x, y, y);
    }
    
    public vec3u64 yyy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec3(y, y, y);
    }
    
    public vec4u64 xxxx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec4(x, x, x, x);
    }
    
    public vec4u64 yxxx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec4(y, x, x, x);
    }
    
    public vec4u64 xyxx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec4(x, y, x, x);
    }
    
    public vec4u64 yyxx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec4(y, y, x, x);
    }
    
    public vec4u64 xxyx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec4(x, x, y, x);
    }
    
    public vec4u64 yxyx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec4(y, x, y, x);
    }
    
    public vec4u64 xyyx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec4(x, y, y, x);
    }
    
    public vec4u64 yyyx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec4(y, y, y, x);
    }
    
    public vec4u64 xxxy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec4(x, x, x, y);
    }
    
    public vec4u64 yxxy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec4(y, x, x, y);
    }
    
    public vec4u64 xyxy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec4(x, y, x, y);
    }
    
    public vec4u64 yyxy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec4(y, y, x, y);
    }
    
    public vec4u64 xxyy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec4(x, x, y, y);
    }
    
    public vec4u64 yxyy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec4(y, x, y, y);
    }
    
    public vec4u64 xyyy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec4(x, y, y, y);
    }
    
    public vec4u64 yyyy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec4(y, y, y, y);
    }
    
}


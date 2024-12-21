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
public partial struct vec2i32{
    internal Vector64<Int32> Data;
    
    public Int32 x {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => Data[0];
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleSetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        set {
            Data = Vector64.Create(value, Data[1]);
        }
        
    }
    
    public Int32 y {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => Data[1];
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleSetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        set {
            Data = Vector64.Create(Data[0], value);
        }
        
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2i32 operator -(vec2i32 v)
    {
        return new() { Data = - v.Data };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2i32 operator +(vec2i32 left, vec2i32 right)
    {
        return new() { Data = left.Data + right.Data };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2i32 operator -(vec2i32 left, vec2i32 right)
    {
        return new() { Data = left.Data - right.Data };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2i32 operator *(vec2i32 left, vec2i32 right)
    {
        return new() { Data = left.Data * right.Data };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2i32 operator /(vec2i32 left, vec2i32 right)
    {
        return new() { Data = left.Data / right.Data };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2i32 operator %(vec2i32 left, vec2i32 right)
    {
        return vec2((Int32)(left.x % right.x), (Int32)(left.y % right.y));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2i32 operator +(Int32 left, vec2i32 right)
    {
        return new() { Data = Vector64.Create(left) + right.Data };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2i32 operator +(vec2i32 left, Int32 right)
    {
        return new() { Data = left.Data + Vector64.Create(right) };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2i32 operator -(Int32 left, vec2i32 right)
    {
        return new() { Data = Vector64.Create(left) - right.Data };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2i32 operator -(vec2i32 left, Int32 right)
    {
        return new() { Data = left.Data - Vector64.Create(right) };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2i32 operator *(Int32 left, vec2i32 right)
    {
        return new() { Data = Vector64.Create(left) * right.Data };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2i32 operator *(vec2i32 left, Int32 right)
    {
        return new() { Data = left.Data * Vector64.Create(right) };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2i32 operator /(Int32 left, vec2i32 right)
    {
        return new() { Data = Vector64.Create(left) / right.Data };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2i32 operator /(vec2i32 left, Int32 right)
    {
        return new() { Data = left.Data / Vector64.Create(right) };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2i32 operator %(Int32 left, vec2i32 right)
    {
        return vec2((Int32)(left % right.x), (Int32)(left % right.y));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec2i32 operator %(vec2i32 left, Int32 right)
    {
        return vec2((Int32)(left.x % right), (Int32)(left.y % right));
    }
    
    public vec2i32 xx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec2(x, x);
    }
    
    public vec2i32 yx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec2(y, x);
    }
    
    public vec2i32 xy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec2(x, y);
    }
    
    public vec2i32 yy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec2(y, y);
    }
    
    public vec3i32 xxx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec3(x, x, x);
    }
    
    public vec3i32 yxx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec3(y, x, x);
    }
    
    public vec3i32 xyx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec3(x, y, x);
    }
    
    public vec3i32 yyx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec3(y, y, x);
    }
    
    public vec3i32 xxy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec3(x, x, y);
    }
    
    public vec3i32 yxy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec3(y, x, y);
    }
    
    public vec3i32 xyy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec3(x, y, y);
    }
    
    public vec3i32 yyy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec3(y, y, y);
    }
    
    public vec4i32 xxxx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec4(x, x, x, x);
    }
    
    public vec4i32 yxxx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec4(y, x, x, x);
    }
    
    public vec4i32 xyxx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec4(x, y, x, x);
    }
    
    public vec4i32 yyxx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec4(y, y, x, x);
    }
    
    public vec4i32 xxyx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec4(x, x, y, x);
    }
    
    public vec4i32 yxyx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec4(y, x, y, x);
    }
    
    public vec4i32 xyyx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec4(x, y, y, x);
    }
    
    public vec4i32 yyyx {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x])]
        get => vec4(y, y, y, x);
    }
    
    public vec4i32 xxxy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec4(x, x, x, y);
    }
    
    public vec4i32 yxxy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec4(y, x, x, y);
    }
    
    public vec4i32 xyxy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec4(x, y, x, y);
    }
    
    public vec4i32 yyxy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec4(y, y, x, y);
    }
    
    public vec4i32 xxyy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec4(x, x, y, y);
    }
    
    public vec4i32 yxyy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec4(y, x, y, y);
    }
    
    public vec4i32 xyyy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.x, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec4(x, y, y, y);
    }
    
    public vec4i32 yyyy {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RuntimeVectorSwizzleGetMethodAttribute([DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y, DualDrill.CLSL.Language.AbstractSyntaxTree.Expression.SwizzleComponent.y])]
        get => vec4(y, y, y, y);
    }
    
}


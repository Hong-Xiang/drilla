using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.Mathematics;
using System.Numerics;
using static DualDrill.Mathematics.DMath;
using vec3 = DualDrill.Mathematics.vec3f32;
using vec2 = DualDrill.Mathematics.vec2f32;

namespace DualDrill.CLSL.Test.ShaderModule;

internal sealed class DevelopShaderModule : ISharpShader
{
    // [Vertex]
    // public static bool ImplicitConvertUIntMax(uint a)
    // {
    //     return a == uint.MaxValue;
    // }

    static vec3 map(vec3 pos)
    {
        return pos;
    }

    static int foo(int x)
    {
        return x;
    }

    static vec3 foov(vec3 x)
    {
        return x;
    }

    [ShaderMethod]
    static float bar(vec3 x)
    {
        return foov(x).x + foov(x).x;
    }

    // [ShaderMethod]
    static vec3f32 calcNormal(vec3f32 pos)
    {
        var e = vec2(1.0f, -1.0f) * 0.5773f * 0.0005f;
        return normalize(e.xyy * map(pos + e.xyy).x +
                         e.yyx * map(pos + e.yyx).x +
                         e.yxy * map(pos + e.yxy).x +
                         e.xxx * map(pos + e.xxx).x);
        //var e = vec2(1.0f, -1.0f) * 0.5773f * 0.0005f;
        //return Normalize(
        //    e.xyy * map(pos + e.xyy).X + e.yyx * map(pos + e.yyx).X + e.yxy * map(pos + e.yxy).X + e.xxx * map
        //    new Vector3(e.X, e.Y, e.Y) * map(pos + new Vector3(e.X, e.Y, e.Y)).X +
        //    new Vector3(e.Y, e.Y, e.X) * map(pos + new Vector3(e.Y, e.Y, e.X)).X +
        //    new Vector3(e.Y, e.X, e.Y) * map(pos + new Vector3(e.Y, e.X, e.Y)).X +
        //new Vector3(e.X, e.X, e.X) * map(pos + new Vector3(e.X, e.X, e.X)).X);
        //vec3 n = vec3(0.0f);
        //for (int i = 0; i < 4; i++)
        //{
        //    vec3 e = 0.5773 * (2.0f * vec3((((i + 3) >> 1) & 1), ((i >> 1) & 1), (i & 1)) - 1.0);
        //    n += e * map(pos + 0.0005f * e).x;
        //    //if( n.x+n.y+n.z>100.0 ) break;
        //}
        //return normalize(n);
    }


    // [Vertex]
    // public static int SimpleLoop(int x)
    // {
    //     for (var i = 0; i < 300; i++)
    //     {
    //         x += 5;
    //     }
    //
    //     return x;
    // }

    //[Vertex]
    //static float SimpleConditionalSwizzle(vec3 p, bool cond)
    //{
    // IL_0000: nop
    // IL_0001: ldarga.s     p
    // IL_0003: ldarg.1      // cond
    // IL_0004: brtrue.s     IL_000f
    //   outputs [ addr<vec3> ] <- stack-top

    //   inputs [ addr<vec3> ]
    // IL_0006: ldarga.s     p
    // IL_0008: call         instance valuetype [DualDrill.Mathematics]DualDrill.Mathematics.vec2f32 [DualDrill.Mathematics]DualDrill.Mathematics.vec3f32::get_xz()
    // IL_000d: br.s         IL_0016
    //   outputs [ addr<vec3>, vec2 ] <- stack-top

    //   inputs [ addr<vec3> ]
    // IL_000f: ldarga.s     p
    // IL_0011: call         instance valuetype [DualDrill.Mathematics]DualDrill.Mathematics.vec2f32 [DualDrill.Mathematics]DualDrill.Mathematics.vec3f32::get_zx()
    //   outputs [ addr<vec3>, vec2 ] <- stack-top

    // IL_0016: call         instance void [DualDrill.Mathematics]DualDrill.Mathematics.vec3f32::set_xz(valuetype [DualDrill.Mathematics]DualDrill.Mathematics.vec2f32)
    // IL_001b: nop
    // IL_001c: ldarga.s     p
    // IL_001e: call         instance float32 [DualDrill.Mathematics]DualDrill.Mathematics.vec3f32::get_x()
    // IL_0023: stloc.0      // V_0
    // IL_0024: br.s         IL_0026

    // IL_0026: ldloc.0      // V_0
    //  IL_0027: ret

    //    p.xz = cond ? p.zx : p.xz;
    //    return p.x;
    //}

    // [Vertex]
    // public static float sdTriPrism(vec3 pa, vec2 ha)
    // {
    //     var p = pa;
    //     var h = ha;
    //     float k = sqrt(3.0f);
    //     h.x = h.x * 0.5f * k;
    //     p.xy = p.xy / h.x;
    //     p.x = abs(p.x) - 1.0f;
    //     p.y = p.y + 1.0f / k;
    //     if (p.x + k * p.y > 0.0)
    //     {
    //         p.xy = vec2(p.x - k * p.y, -k * p.x - p.y) / 2.0f;
    //     }
    //
    //     p.x = p.x - clamp(p.x, -2.0f, 0.0f);
    //     float d1 = length(p.xy) * sign(-p.y) * h.x;
    //     float d2 = abs(p.z) - h.y;
    //     return length(max(vec2(d1, d2), vec2(0.0f))) + min(max(d1, d2), 0.0f);
    // }

    // [Vertex]
    // public static int LoopWithInnerConditionalBreak(int x)
    // {
    //     for (var i = 0; i < 300; i++)
    //     {
    //         if (x > 2048)
    //         {
    //             break;
    //         }
    //
    //         x += 5;
    //     }
    //
    //     return x;
    // }

    // static bool SomeCond()
    // {
    //     return false;
    // }
    //
    // [Vertex]
    // static vec3f32 render(vec3f32 ro, vec3f32 rd, vec3f32 rdx, vec3f32 rdy)
    // {
    //     // background
    //     var col = vec3(0.0f);
    //
    //     // raycast scene
    //     // var res = vec2(1.0f, 2.0f);
    //     // var t = res.x;
    //     // var m = res.y;
    //     // if (m > -0.5f)
    //     // if (SomeCond())
    //     // {
    //         // var nor = vec3(0.0f, 1.0f, 0.0f);
    //         //if (m < 1.5f)
    //         //{
    //         //    nor = new Vector3(0.0f, 1.0f, 0.0f);
    //         //}
    //         // if (m >= 1.5f)
    //         // if (SomeCond())
    //         // {
    //         //     col = vec3(2.0f);
    //         // }
    //         //
    //         // material        
    //
    //
    //         if (SomeCond())
    //         // if (m < 1.5f)
    //         {
    //             // project pixel footprint into the plane
    //             // var dpdx = ro.y * (rd / rd.y - rdx / rdx.y);
    //             // var dpdy = ro.y * (rd / rd.y - rdy / rdy.y);
    //             //
    //             // var f = 0.5f;
    //             col = vec3(3.0f);
    //         }
    //         else
    //         {
    //             col = vec3(4.0f);
    //         }
    //     // }
    //
    //     return col;
    // }
}

internal sealed class DevelopTestShaderModule
    : ISharpShader
{
  public  static vec3 Foo(vec3 pos)
    {
        return pos;
    }
  public  static vec3f32 NestedExpressionWithFunctionCall(vec3f32 pos)
    {
        var e = vec2(1.0f, -1.0f) * 0.5773f * 0.0005f;
        return normalize(e.xyy * Foo(pos + e.xyy).x +
                         e.yyx * Foo(pos + e.yyx).x +
                         e.yxy * Foo(pos + e.yxy).x +
                         e.xxx * Foo(pos + e.xxx).x);
        //var e = vec2(1.0f, -1.0f) * 0.5773f * 0.0005f;
        //return Normalize(
        //    e.xyy * map(pos + e.xyy).X + e.yyx * map(pos + e.yyx).X + e.yxy * map(pos + e.yxy).X + e.xxx * map
        //    new Vector3(e.X, e.Y, e.Y) * map(pos + new Vector3(e.X, e.Y, e.Y)).X +
        //    new Vector3(e.Y, e.Y, e.X) * map(pos + new Vector3(e.Y, e.Y, e.X)).X +
        //    new Vector3(e.Y, e.X, e.Y) * map(pos + new Vector3(e.Y, e.X, e.Y)).X +
        //new Vector3(e.X, e.X, e.X) * map(pos + new Vector3(e.X, e.X, e.X)).X);
        //vec3 n = vec3(0.0f);
        //for (int i = 0; i < 4; i++)
        //{
        //    vec3 e = 0.5773 * (2.0f * vec3((((i + 3) >> 1) & 1), ((i >> 1) & 1), (i & 1)) - 1.0);
        //    n += e * map(pos + 0.0005f * e).x;
        //    //if( n.x+n.y+n.z>100.0 ) break;
        //}
        //return normalize(n);
    }

    [Vertex]
    public static int NestedLoopFlat(int x, int y, int nx, int ny)
    {
        var result = 0;
        for (var i = 0; i < nx * ny; i++)
        {
            var ix = i / ny;
            var iy = i % ny;
            result += y;

            result += x;
        }

        return result;
    }
    [Vertex]
    public static uint StackTransferedValuesWithLiteralImplicitConversion(uint x, uint y)
    {
        return x >= y ? x : uint.MaxValue;
    }

    [Vertex]
    public static int NestedLoop(int x, int y, int nx, int ny)
    {
        var result = 0;
        for (var ix = 0; ix < nx; ix++)
        {
            for (var iy = 0; iy < ny; iy++)
            {
                result += y;
            }

            result += x;
        }

        return result;
    }

    [Vertex]
    public static int SimpleLoop(int x)
    {
        for (var i = 0; i < 300; i++)
        {
            x += 5;
        }

        return x;
    }

    [Vertex]
    public static int LoopWithInnerConditionalBreak(int x)
    {
        for (var i = 0; i < 300; i++)
        {
            if (x > 2048)
            {
                break;
            }

            x += 5;
        }

        return x;
    }

    [Vertex]
    public static int MinimumIfThenElse(int a)
    {
        if (a >= 42)
        {
            return 1;
        }
        else
        {
            return 2;
        }
    }

    [Vertex]
    public static bool ImplicitConvert(uint a)
    {
        return a == 1;
    }

    [Vertex]
    public static bool ImplicitConvertUIntMax(uint a)
    {
        return a == uint.MaxValue;
    }

    [Vertex]
    public static int MaxByIfThenElse(int a, int b)
    {
        if (a >= b)
        {
            return a;
        }
        else
        {
            return b;
        }
    }

    [Vertex]
    public static int MaxByTernaryOperator(int a, int b)
    {
        return a >= b ? a : b;
    }

    [Vertex]
    public static vec2f32 BroadcastVectorOperation(vec2f32 a, float b)
        => a * b;

    [Vertex]
    public static vec2f32 SetComponent(float x, float y)
    {
        var v = vec2(0.0f);
        v.x = x;
        v.y = y;
        return v;
    }

    [Vertex]
    public static int Return42() => 42;

    [Vertex]
    public static int LoadArg(int a) => a;

    [Vertex]
    public static int APlus1(int a) => a + 1;

    [Vertex]
    public static vec3f32 VecSwizzleGetter(vec2f32 v) => v.xyx;

    [Vertex]
    public static vec4f32 VecSwizzleSetter(vec4f32 a, vec2f32 b)
    {
        a.xy = b;
        return a;
    }

    [Vertex]
    public static Vector4 SystemNumericVector4Creation() => new Vector4(1.0f, 2.0f, 3.0f, 4.0f);


    public static int Add(int a, int b) => a + b;

    [Vertex]
    public static int MethodInvocation() => Add(1, 2);

    [Vertex]
    public static int APlusB(int a, int b) => a + b;

    [Vertex]
    public static float Vector4Dot(Vector4 a, Vector4 b) => Vector4.Dot(a, b);

    // ternary conditional swizzle 
    // this case is complex due to passing ptr<vec3> across basic block boundary via evaluation stack
    // while in many target IR
    // declare a local variable for general ptr type is not allowed
    [Vertex]
    public static float TernaryConditionalSwizzle(vec3 p, bool cond)
    {
        // IL_0000: nop
        // IL_0001: ldarga.s     p
        // IL_0003: ldarg.1      // cond
        // IL_0004: brtrue.s     IL_000f
        //   outputs [ addr<vec3> ] <- stack-top
        //
        //   inputs [ addr<vec3> ]
        // IL_0006: ldarga.s     p
        // IL_0008: call         instance valuetype [DualDrill.Mathematics]DualDrill.Mathematics.vec2f32 [DualDrill.Mathematics]DualDrill.Mathematics.vec3f32::get_xz()
        // IL_000d: br.s         IL_0016
        //   outputs [ addr<vec3>, vec2 ] <- stack-top
        //
        //   inputs [ addr<vec3> ]
        // IL_000f: ldarga.s     p
        // IL_0011: call         instance valuetype [DualDrill.Mathematics]DualDrill.Mathematics.vec2f32 [DualDrill.Mathematics]DualDrill.Mathematics.vec3f32::get_zx()
        //   outputs [ addr<vec3>, vec2 ] <- stack-top
        //
        // IL_0016: call         instance void [DualDrill.Mathematics]DualDrill.Mathematics.vec3f32::set_xz(valuetype [DualDrill.Mathematics]DualDrill.Mathematics.vec2f32)
        // IL_001b: nop
        // IL_001c: ldarga.s     p
        // IL_001e: call         instance float32 [DualDrill.Mathematics]DualDrill.Mathematics.vec3f32::get_x()
        // IL_0023: stloc.0      // V_0
        // IL_0024: br.s         IL_0026
        //
        // IL_0026: ldloc.0      // V_0
        //  IL_0027: ret
        //
        p.xz = cond ? p.zx : p.xz;
        return p.x;
    }

    //[Uniform]
    //[Group(0)]
    //[Binding(0)]
    //public vec4f32 UniformValue;

    //[Vertex]
    //public vec4f32 UniformTest()
    //{
    //    return UniformValue;
    //}
}
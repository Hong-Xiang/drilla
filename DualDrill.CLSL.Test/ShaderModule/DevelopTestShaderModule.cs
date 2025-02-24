using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.Mathematics;
using System.Numerics;

namespace DualDrill.CLSL.Test.ShaderModule;

internal sealed class DevelopShaderModule : ISharpShader
{
    // [Vertex]
    // public static bool ImplicitConvertUIntMax(uint a)
    // {
    //     return a == uint.MaxValue;
    // }

    [Vertex]
    public static int SimpleLoop(int x)
    {
        for (var i = 0; i < 300; i++)
        {
            x += 5;
        }

        return x;
    }
}

internal sealed class DevelopTestShaderModule
    : ISharpShader
{
    [Vertex]
    public static uint StackTransferedValuesWithLiteralImplicitConversion(uint x, uint y)
    {
        return x >= y ? x : uint.MaxValue;
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
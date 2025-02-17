﻿using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.Mathematics;
using System.Numerics;

namespace DualDrill.CLSL.Test.ShaderModule;

internal sealed class DevelopTestShaderModule
    : ISharpShader
{
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
    public static int MaxByfThenElse(int a, int b)
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
    public static int MaxByTaneryOperator(int a, int b)
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
    public static Vector4 SystemNumericVector4Creation() => new Vector4(1.0f, 2.0f, 3.0f, 4.0f);


    public static int Add(int a, int b) => a + b;

    [Vertex]
    public static int MethodInvocation() => Add(1, 2);

    [Vertex]
    public static int APlusB(int a, int b) => a + b;

    [Vertex]
    public static int StaticTest() => 42;

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
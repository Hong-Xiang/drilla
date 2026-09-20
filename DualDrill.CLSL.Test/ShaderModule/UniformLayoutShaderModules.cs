using System.Runtime.InteropServices;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.Mathematics;
using static DualDrill.Mathematics.DMath;

namespace DualDrill.CLSL.Test.ShaderModule;

internal sealed class UniformLayoutReferenceShaderModule : ISharpShader
{
    public struct Params
    {
        public vec4f32 Tint;
        public float Exposure;
        public uint Mode;
        public vec2f32 Padding;
    }

    public struct Tail
    {
        public vec3f32 Direction;
        public float Weight;
        public vec2f32 Offset;
    }

    [Group(1)]
    [Binding(2, HasDynamicOffset: true)]
    [Vertex]
    [Fragment]
    [Uniform]
    private static readonly Params Settings;

    [Group(1)]
    [Binding(3)]
    [Uniform]
    private static readonly Tail Extra;

    [Fragment]
    [return: Location(0)]
    public static vec4f32 Shade() =>
        Settings.Tint * Settings.Exposure
        + vec4(Extra.Direction * Extra.Weight, Extra.Weight)
        + vec4(Settings.Padding, Extra.Offset);
}

internal sealed class UniformLayoutProfileShaderModule : ISharpShader
{
    public struct ScalarOnly
    {
        public float Value;
    }

    public struct Vec3ThenScalar
    {
        public vec3f32 Vector;
        public float Scalar;
    }

    [Group(0)][Binding(0)][Uniform] private static readonly float F32;
    [Group(0)][Binding(1)][Uniform] private static readonly vec2f32 F32x2;
    [Group(0)][Binding(2)][Uniform] private static readonly vec3f32 F32x3;
    [Group(0)][Binding(3)][Uniform] private static readonly vec4f32 F32x4;
    [Group(0)][Binding(4)][Uniform] private static readonly int I32;
    [Group(0)][Binding(5)][Uniform] private static readonly vec2i32 I32x2;
    [Group(0)][Binding(6)][Uniform] private static readonly vec3i32 I32x3;
    [Group(0)][Binding(7)][Uniform] private static readonly vec4i32 I32x4;
    [Group(2)][Binding(0)][Uniform] private static readonly uint U32;
    [Group(2)][Binding(1)][Uniform] private static readonly vec2u32 U32x2;
    [Group(2)][Binding(2)][Uniform] private static readonly vec3u32 U32x3;
    [Group(2)][Binding(3)][Uniform] private static readonly vec4u32 U32x4;
    [Group(2)][Binding(4)][Uniform] private static readonly ScalarOnly ScalarStruct;
    [Group(2)][Binding(5)][Uniform] private static readonly Vec3ThenScalar PackedStruct;

    [Fragment]
    [return: Location(0)]
    public static vec4f32 Shade() =>
        vec4(F32 + F32x2.x + F32x3.x + F32x4.x);

    [Fragment][return: Location(0)] public static int ShadeI32() => I32;
    [Fragment][return: Location(0)] public static vec2i32 ShadeI32x2() => I32x2;
    [Fragment][return: Location(0)] public static vec3i32 ShadeI32x3() => I32x3;
    [Fragment][return: Location(0)] public static vec4i32 ShadeI32x4() => I32x4;
    [Fragment][return: Location(0)] public static uint ShadeU32() => U32;
    [Fragment][return: Location(0)] public static vec2u32 ShadeU32x2() => U32x2;
    [Fragment][return: Location(0)] public static vec3u32 ShadeU32x3() => U32x3;
    [Fragment][return: Location(0)] public static vec4u32 ShadeU32x4() => U32x4;
    [Fragment][return: Location(0)] public static float ShadeScalarStruct() => ScalarStruct.Value;

    [Fragment]
    [return: Location(0)]
    public static vec4f32 ShadePackedStruct() =>
        vec4(PackedStruct.Vector, PackedStruct.Scalar);
}

internal static class UnsupportedUniformShaders
{
    internal struct NestedInner
    {
        public float Value;
    }

    internal struct NestedOuter
    {
        public NestedInner Value;
    }

    internal struct Empty
    {
    }

    internal struct Property
    {
        public float Field;
        public float Computed => Field;
    }

    internal struct MemberAlign
    {
        [Align(16)] public float Field;
    }

    [Align(16)]
    internal struct StructureAlign
    {
        public float Field;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct Explicit
    {
        [FieldOffset(0)] public float Field;
    }

    internal sealed class VariableAlign : ISharpShader
    {
        [Group(0)]
        [Binding(0)]
        [Uniform]
        [Align(16)]
        private static readonly float Value;

        [Fragment]
        [return: Location(0)]
        public static vec4f32 Shade() => vec4(0.0f);
    }

    internal sealed class Shader<T> : ISharpShader
    {
        [Group(0)]
        [Binding(0)]
        [Uniform]
        private static readonly T Value = default!;

        [Fragment]
        [return: Location(0)]
        public static vec4f32 Shade() => vec4(0.0f);
    }
}

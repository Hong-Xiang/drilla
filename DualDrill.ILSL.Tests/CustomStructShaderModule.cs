using DualDrill.CLSL.Language.IR.ShaderAttribute;
using DualDrill.ILSL.Frontend;
using DualDrill.Mathematics;

namespace DualDrill.ILSL.Tests;

internal class CustomStructShaderModule
{
    internal struct CustomStageData
    {
        public float Value1;
        public float Value2 { get; set; }
    }

    [Vertex]
    public static CustomStageData vs()
    {
        return new CustomStageData { Value1 = 1.0f, Value2 = 2.0f };
    }

    [Fragment]
    public static vec4f32 fs(CustomStageData data)
    {
        return DMath.vec4(1.0f, 1.0f, 1.0f, 1.0f);
    }
}

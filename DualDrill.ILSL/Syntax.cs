using System.Numerics;
using Silk.NET.Maths;

namespace DualDrill.CLSL;

//public sealed class ShaderMethodAttribute() : Attribute, IShaderStageAttribute
//{
//}

//public sealed class BindingAttribute(int Binding, bool HasDynamicOffset = false) : Attribute, IShaderAttribute
//{
//    public int Binding { get; } = Binding;
//    public bool HasDynamicOffset { get; } = HasDynamicOffset;
//}

//public sealed class UniformAttribute() : Attribute, IShaderAttribute { }
//public sealed class ReadAttribute() : Attribute, IShaderAttribute { }
//public sealed class ReadWriteAttribute() : Attribute, IShaderAttribute { }
//public sealed class StageAttribute(GPUShaderStage stage) : Attribute, IShaderAttribute
//{
//    public GPUShaderStage Stage { get; } = stage;
//}
//public sealed class VertexStepModeAttribute(GPUVertexStepMode StepMode) : Attribute
//{
//    public GPUVertexStepMode StepMode { get; } = StepMode;
//}

public interface ISharpShader
{
}

public interface ISampler
{
}

public interface ITexture2D<T>
{
    public T Sample(ISampler sampler, Vector2 coordinate);
    public T Sample(ISampler sampler, Vector2D<float> coordinate);
}
using DualDrill.Graphics;

namespace DualDrill.CLSL.Language.ShaderAttribute;

public sealed class VertexStepModeAttribute(GPUVertexStepMode StepMode) : Attribute
{
    public GPUVertexStepMode StepMode { get; } = StepMode;
}

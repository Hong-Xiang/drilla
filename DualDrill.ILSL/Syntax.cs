using DualDrill.Graphics;
using DualDrill.ILSL.IR;

namespace DualDrill.ILSL;

public enum BuiltinBinding
{
    vertex_index,
    instance_index,
    position,
    front_facing,
    frag_depth,
    sample_index,
    local_invocation_id,
    local_invocation_index,
    global_invocation_id,
    workgroup_id,
    num_workgroups,
}

public sealed class BuiltinAttribute(BuiltinBinding Slot) : Attribute, IAttribute
{
    public BuiltinBinding Slot { get; } = Slot;
}

public sealed class VertexAttribute() : Attribute, IAttribute { }
public sealed class FragmentAttribute() : Attribute, IAttribute { }
public sealed class ComputeAttribute() : Attribute, IAttribute { }

public sealed class LocationAttribute(int Binding) : Attribute, IAttribute
{
    public int Binding { get; } = Binding;
}

public sealed class GroupAttribute(int Binding) : Attribute, IAttribute
{
    public int Binding { get; } = Binding;
}

public sealed class BindingAttribute(int Binding, bool HasDynamicOffset = false) : Attribute, IAttribute
{
    public int Binding { get; } = Binding;
    public bool HasDynamicOffset { get; } = HasDynamicOffset;
}

public sealed class UniformAttribute() : Attribute, IAttribute { }
public sealed class ReadAttribute() : Attribute, IAttribute { }
public sealed class ReadWriteAttribute() : Attribute, IAttribute { }
public sealed class StageAttribute(GPUShaderStage stage) : Attribute, IAttribute
{
    public GPUShaderStage Stage { get; } = stage;
}
public sealed class VertexStepModeAttribute(GPUVertexStepMode StepMode) : Attribute
{
    public GPUVertexStepMode StepMode { get; } = StepMode;
}


public interface IShaderModule
{
}

public sealed class ShaderMethodAttribute() : Attribute
{
}

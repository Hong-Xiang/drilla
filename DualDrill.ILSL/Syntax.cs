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
    [Obsolete($"use {nameof(vertex_index)}")]
    VertexIndex,
    [Obsolete($"use {nameof(instance_index)}")]
    InstanceIndex,
    [Obsolete($"use {nameof(position)}")]
    Position,
    [Obsolete($"use {nameof(front_facing)}")]
    FrontFacing,
}

public sealed class BuiltinAttribute(BuiltinBinding Slot) : Attribute
{
    public BuiltinBinding Slot { get; } = Slot;
}

public sealed class VertexAttribute() : Attribute { }
public sealed class FragmentAttribute() : Attribute { }

public sealed class LocationAttribute(int Binding) : Attribute
{
    public int Binding { get; } = Binding;
}

public interface IShaderModule
{
}

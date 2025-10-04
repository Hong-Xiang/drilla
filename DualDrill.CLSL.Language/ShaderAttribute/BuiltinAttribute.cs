namespace DualDrill.CLSL.Language.ShaderAttribute;

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
    num_workgroups
}

public sealed class BuiltinAttribute(BuiltinBinding Slot) : Attribute, ISemanticBindingAttribute
{
    public BuiltinBinding Slot { get; } = Slot;
}
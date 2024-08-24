namespace DualDrill.ILSL;

public enum BuiltinBinding
{
    Position,
    VertexIndex
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

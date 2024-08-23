namespace BaiscWebApp.WGSLGen;

enum BuiltinBinding
{
    Position,
    VertexIndex
}

sealed class BuiltinAttribute(BuiltinBinding Slot) : Attribute
{
    public BuiltinBinding Slot { get; } = Slot;
}

sealed class VertexAttribute() : Attribute { }
sealed class FragmentAttribute() : Attribute { }

sealed class LocationAttribute(int Binding) : Attribute
{
    public int Binding { get; } = Binding;
}

public interface IShaderModule
{
}

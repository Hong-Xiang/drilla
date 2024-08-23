using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ILSLPrototype;

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


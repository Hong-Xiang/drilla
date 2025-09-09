using DualDrill.CLSL.Language.Symbol;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.ShaderAttribute;

public sealed record class EntryPointInterfaceValuesShaderAttribute(
    ImmutableArray<IShaderValue> InterfaceValues
) : IShaderAttribute
{
}

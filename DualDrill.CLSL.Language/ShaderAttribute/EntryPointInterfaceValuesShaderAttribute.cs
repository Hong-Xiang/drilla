using System.Collections.Immutable;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language.ShaderAttribute;

public sealed record class EntryPointInterfaceValuesShaderAttribute(
    ImmutableArray<IShaderValue> InterfaceValues
) : IShaderAttribute
{
}
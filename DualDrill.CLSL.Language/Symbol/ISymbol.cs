using System.Collections.Immutable;
using DualDrill.CLSL.Language.ShaderAttribute;

namespace DualDrill.CLSL.Language.Symbol;

public interface ISymbol
{
    string Name { get; }
    ImmutableArray<IShaderAttribute> Attributes { get; }
}
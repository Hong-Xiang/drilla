using DualDrill.CLSL.Language.ShaderAttribute;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.Symbol;

public interface ISymbol
{
    string Name { get; }
    ImmutableArray<IShaderAttribute> Attributes { get; }
}
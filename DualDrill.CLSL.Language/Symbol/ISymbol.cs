using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.Symbol;

public interface ISymbol
{
    string Name { get; }
    ImmutableArray<IShaderAttribute> Attributes { get; }
}

public interface IShaderSymbol<out T> : ISymbol
{
}

internal sealed class PrimitiveTypeSymbol<TShaderType>
    where TShaderType : IShaderType, ISingleton<TShaderType>
{
}

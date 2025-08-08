using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Frontend.ReflectionShader;

public sealed class CilFunctionBody : IFunctionBodySymbol
{
    public ImmutableArray<ILocalVariableSymbol> LocalVariables => throw new NotImplementedException();

    public ImmutableArray<Label> Labels => throw new NotImplementedException();

    public string Name => string.Empty;

    public ImmutableArray<IShaderAttribute> Attributes => [];
}

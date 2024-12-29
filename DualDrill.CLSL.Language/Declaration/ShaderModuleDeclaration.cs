using DualDrill.CLSL.Language.ShaderAttribute;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.Declaration;

public sealed record class ShaderModuleDeclaration(
    ImmutableArray<IDeclaration> Declarations,
    ImmutableDictionary<FunctionDeclaration, IFunctionBody> FunctionDefinitions)
    : IDeclaration
{
    public string Name => nameof(ShaderModuleDeclaration);
    public ImmutableHashSet<IShaderAttribute> Attributes => [];

    public IEnumerable<FunctionDeclaration> DefinedFunctions => FunctionDefinitions.Keys;

    public IFunctionBody GetBody(FunctionDeclaration func)
    {
        return FunctionDefinitions[func];
    }

    public static ShaderModuleDeclaration Empty
           => new([], ImmutableDictionary<FunctionDeclaration, IFunctionBody>.Empty);
}

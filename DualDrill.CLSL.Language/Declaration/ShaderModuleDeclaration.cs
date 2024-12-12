using DualDrill.CLSL.Language.ShaderAttribute;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.Declaration;

public sealed record class ShaderModuleDeclaration(ImmutableArray<IDeclaration> Declarations) : IDeclaration
{
    public string Name => nameof(ShaderModuleDeclaration);
    public ImmutableHashSet<IShaderAttribute> Attributes => [];
}

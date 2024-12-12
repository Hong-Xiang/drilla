using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Declaration;

public sealed record class TypeDeclaration(IShaderType Type, ImmutableHashSet<ShaderAttribute.IShaderAttribute> Attributes) : IDeclaration
{
    public string Name => Type.Name;
}

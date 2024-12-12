using System.Collections.Immutable;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Declaration;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree;

public sealed record class ShaderModule(ImmutableArray<IDeclaration> Declarations) : IShaderAstNode
{
}

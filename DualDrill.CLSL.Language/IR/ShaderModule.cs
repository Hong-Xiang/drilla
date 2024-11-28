using System.Collections.Immutable;
using DualDrill.CLSL.Language.IR.Declaration;

namespace DualDrill.CLSL.Language.IR;

public sealed record class ShaderModule(ImmutableArray<IDeclaration> Declarations) : IShaderAstNode
{
}

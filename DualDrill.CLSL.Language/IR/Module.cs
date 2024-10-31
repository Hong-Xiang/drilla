using System.Collections.Immutable;
using DualDrill.CLSL.Language.IR.Declaration;

namespace DualDrill.CLSL.Language.IR;

public sealed record class Module(ImmutableArray<IDeclaration> Declarations) : IAstNode
{
}

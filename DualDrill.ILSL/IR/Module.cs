using System.Collections.Immutable;
using DualDrill.ILSL.IR.Declaration;

namespace DualDrill.ILSL.IR;

public sealed record class Module(ImmutableArray<IDeclaration> Declarations) : INode
{
}

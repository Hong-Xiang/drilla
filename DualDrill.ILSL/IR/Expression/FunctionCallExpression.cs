using DualDrill.ILSL.IR.Declaration;
using System.Collections.Immutable;

namespace DualDrill.ILSL.IR.Expression;

public sealed record class FunctionCallExpression(FunctionDeclaration Callee, ImmutableArray<IExpression> Arguments) : IExpression
{
}

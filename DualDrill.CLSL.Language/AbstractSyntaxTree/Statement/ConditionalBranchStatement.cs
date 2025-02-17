using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

public sealed record class ConditionalBranchStatement(
    IExpression Condition
) : IUnstructuredStackStatement
{
}
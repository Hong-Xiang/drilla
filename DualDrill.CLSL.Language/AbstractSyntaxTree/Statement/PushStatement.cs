using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

public sealed record class PushStatement(IExpression Expr) : IStackStatement
{
    public IEnumerable<Label> ReferencedLabels => [];
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => Expr.ReferencedVariables;

    public IEnumerable<IStackInstruction> ToInstructions()
        => Expr.ToInstructions();
}
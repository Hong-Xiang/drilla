using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

/// <summary>
/// Evaluate expression and discard its result,
/// ExpressionStatement, or PhonyAssignmentStatement are used to represent:
/// * calling void type functions
/// * pop instruction
/// </summary>
/// <param name="Expr"></param>
public sealed record class PhonyAssignmentStatement(
    IExpression Expr
) : IStatement, IStackStatement, IForInit, IForUpdate
{
    public IEnumerable<Label> ReferencedLabels => [];
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => Expr.ReferencedVariables;

    public IEnumerable<IInstruction> ToInstructions()
        => [..Expr.ToInstructions(), ShaderInstruction.Pop()];

    public T Accept<T>(IStatementVisitor<T> visitor)
        => visitor.VisitPhonyAssignment(this);

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.Write("\t");
        using (writer.IndentedScope())
        {
            Expr.Dump(context, writer);
        }

        writer.WriteLine(";");
    }
}
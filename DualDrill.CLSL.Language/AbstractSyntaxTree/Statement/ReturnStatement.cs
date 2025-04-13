using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

public sealed record class ReturnStatement(IExpression? Expr) : IStatement, IStackStatement
{
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => Expr?.ReferencedVariables ?? [];

    public IEnumerable<IInstruction> ToInstructions()
        => [..Expr.ToInstructions(), ShaderInstruction.ReturnResult()];

    public T Accept<T>(IStatementVisitor<T> visitor)
        => visitor.VisitReturn(this);

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        if (Expr is null)
        {
            writer.WriteLine("return;");
        }
        else
        {
            writer.WriteLine("return");
            using (writer.IndentedScope())
            {
                Expr.Dump(context, writer);
            }
        }
    }
}
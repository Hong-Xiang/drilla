using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public sealed record class IndirectionExpression(IExpression Expr) : IExpression
{
    public IShaderType Type => ((IPtrType)Expr).BaseType;

    public TResult Accept<TResult>(IExpressionVisitor<TResult> visitor)
        => visitor.VisitIndirectionExpression(this);

    public IEnumerable<IInstruction> ToInstructions()
        => Expr.Type switch
        {
            IPtrType { BaseType: ISingletonShaderType t } =>
            [
                ..Expr.ToInstructions(),
                new IndirectionInstruction(t)
            ],
            _ => throw new NotImplementedException()
        };

    public IEnumerable<VariableDeclaration> ReferencedVariables => [..Expr.ReferencedVariables];

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine("indirection");
        using (writer.IndentedScope())
        {
            Expr.Dump(context, writer);
        }
    }
}
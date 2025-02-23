using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.ShaderAttribute;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

public sealed record class IfStatement(
    IExpression Expr,
    CompoundStatement TrueBody,
    CompoundStatement FalseBody,
    ImmutableHashSet<IShaderAttribute> Attributes
) : IStatement
{
    public T Accept<T>(IStatementVisitor<T> visitor)
        => visitor.VisitIf(this);

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine("if");
        using (writer.IndentedScope())
        {
            writer.WriteLine("condition");
            using (writer.IndentedScope())
            {
                Expr.Dump(context, writer);
            }

            writer.WriteLine("then");
            using (writer.IndentedScope())
            {
                TrueBody.Dump(context, writer);
            }

            writer.WriteLine("else");
            using (writer.IndentedScope())
            {
                FalseBody.Dump(context, writer);
            }
        }
    }

    public IEnumerable<Label> ReferencedLabels =>
    [
        ..TrueBody.ReferencedLabels,
        ..FalseBody.ReferencedLabels
    ];

    public IEnumerable<VariableDeclaration> ReferencedLocalVariables =>
    [
        ..Expr.ReferencedVariables,
        ..TrueBody.ReferencedLocalVariables,
        ..FalseBody.ReferencedLocalVariables
    ];
}
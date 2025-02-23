using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

public sealed record class SwitchStatement(
    IExpression Expr,
    ImmutableHashSet<SwitchCase> Cases,
    CompoundStatement DefaultCase
) : IStatement
{
    public T Accept<T>(IStatementVisitor<T> visitor)
        => visitor.VisitSwitch(this);

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine("switch");
        using (writer.IndentedScope())
        {
            Expr.Dump(context, writer);
        }
    }

    public IEnumerable<Label> ReferencedLabels =>
    [
        ..Cases.SelectMany(c => c.Body.ReferencedLabels),
        ..DefaultCase.ReferencedLabels,
    ];

    public IEnumerable<VariableDeclaration> ReferencedLocalVariables =>
    [
        ..Expr.ReferencedVariables,
        ..Cases.SelectMany(c => c.Body.ReferencedLocalVariables),
        ..DefaultCase.ReferencedLocalVariables
    ];
}

public sealed record class SwitchCase(
    LiteralValueExpression Label,
    CompoundStatement Body
)
{
}
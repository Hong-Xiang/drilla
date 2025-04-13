using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

[Obsolete]
public sealed record class DecrementStatement(
    IExpression Expr
) : IStatement, IForInit, IForUpdate
{
    public T Accept<T>(IStatementVisitor<T> visitor)
        => visitor.VisitDecrement(this);

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        throw new NotImplementedException();
    }
}
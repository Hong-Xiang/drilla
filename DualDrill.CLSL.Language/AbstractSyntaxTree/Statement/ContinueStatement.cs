using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

public sealed class ContinueStatement : IStatement
{
    public T Accept<T>(IStatementVisitor<T> visitor)
        => visitor.VisitContinue(this);

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine("continue");
    }
}
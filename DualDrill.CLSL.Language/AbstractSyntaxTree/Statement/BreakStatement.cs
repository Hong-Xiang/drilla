using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

public sealed record class BreakStatement : IStatement
{
    public T Accept<T>(IStatementVisitor<T> visitor)
        => visitor.VisitBreak(this);

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine("break");
    }

    public IEnumerable<Label> ReferencedLabels => [];
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => [];
}
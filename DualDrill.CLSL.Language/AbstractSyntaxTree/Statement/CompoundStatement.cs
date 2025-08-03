using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.Common.CodeTextWriter;
using System.CodeDom.Compiler;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

public sealed record class CompoundStatement(ImmutableArray<IStatement> Statements)
    : IStatement
{
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables =>
        Statements.SelectMany(s => s.ReferencedLocalVariables);

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine("compound");
        using (writer.IndentedScope())
        {
            foreach (var stmt in Statements)
            {
                stmt.Dump(context, writer);
            }
        }
    }

    public static CompoundStatement Empty => new([]);

    public T Accept<T>(IStatementVisitor<T> visitor)
        => visitor.VisitCompound(this);
}
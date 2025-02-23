using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

public sealed record class LoopStatement(CompoundStatement Body) : IStatement
{
    public T Accept<T>(IStatementVisitor<T> visitor)
        => visitor.VisitLoop(this);

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine("loop");
        using (writer.IndentedScope())
        {
            Body.Dump(context, writer);
        }
    }

    public IEnumerable<Label> ReferencedLabels => Body.ReferencedLabels;
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => Body.ReferencedLocalVariables;
}
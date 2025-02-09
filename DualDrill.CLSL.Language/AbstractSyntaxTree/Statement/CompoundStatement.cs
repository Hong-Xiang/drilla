using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.Common.CodeTextWriter;
using System.CodeDom.Compiler;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

public sealed record class CompoundStatement(ImmutableArray<IStatement> Statements)
    : IStatement
    , IFunctionBodyData
{
    public IEnumerable<VariableDeclaration> LocalVariables =>
        Statements.SelectMany(s =>
        s switch
        {
            CompoundStatement x => x.LocalVariables,
            VariableOrValueStatement x => [x.Variable],
            _ => []
        });

    public IEnumerable<Label> Labels => [];

    public void Dump(IFunctionBody context, IndentedTextWriter writer)
    {
        foreach (var stmt in Statements)
        {
            switch (stmt)
            {
                case CompoundStatement s:
                    using (writer.IndentedScopeWithBracket())
                    {
                        s.Dump(context, writer);
                    }
                    break;
                default:
                    writer.WriteLine(stmt.ToString());
                    break;
            }
        }
    }
}




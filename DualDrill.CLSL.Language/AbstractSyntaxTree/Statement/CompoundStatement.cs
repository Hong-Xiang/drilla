using DualDrill.CLSL.Language.FunctionBody;
using System.CodeDom.Compiler;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

public sealed record class CompoundStatement(ImmutableArray<IStatement> Statements)
    : IStatement
    , IFunctionBody
{
    public void Dump(IndentedTextWriter writer)
    {
        throw new NotImplementedException();
    }
}




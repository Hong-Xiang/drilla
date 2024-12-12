using DualDrill.CLSL.Language.AbstractSyntaxTree.Declaration;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

public sealed record class VariableOrValueStatement(VariableDeclaration Variable) : IStatement, IForInit
{
}




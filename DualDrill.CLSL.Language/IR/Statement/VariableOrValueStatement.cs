using DualDrill.CLSL.Language.IR.Declaration;

namespace DualDrill.CLSL.Language.IR.Statement;

public sealed record class VariableOrValueStatement(VariableDeclaration Variable) : IStatement, IForInit
{
}




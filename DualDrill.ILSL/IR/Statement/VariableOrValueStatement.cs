using DualDrill.ILSL.IR.Declaration;

namespace DualDrill.ILSL.IR.Statement;

public sealed record class VariableOrValueStatement(VariableDeclaration Variable) : IStatement
{
}




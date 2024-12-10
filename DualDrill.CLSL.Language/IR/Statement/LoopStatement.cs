namespace DualDrill.CLSL.Language.IR.Statement;

public sealed record class LoopStatement(CompoundStatement Body) : IStatement
{
}

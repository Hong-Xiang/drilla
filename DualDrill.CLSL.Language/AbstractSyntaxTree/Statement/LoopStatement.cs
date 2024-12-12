namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

public sealed record class LoopStatement(CompoundStatement Body) : IStatement
{
}

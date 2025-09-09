using DualDrill.CLSL.Language.Statement;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Operation;

public interface IStatementOperation : IOperation
{
    IShaderType ResultType { get; }

    TR Evaluate<TV, TE, TS, TR>(IStatementNode<TV, TE> stmt, TS semantic)
        where TS : IStatementSemantic<TV, TE, TR>;
}

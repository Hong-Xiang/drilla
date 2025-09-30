using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Operation;

public interface IBinaryStatementOperation : IOperation
{
    public IShaderType LeftType { get; }
    public IShaderType RightType { get; }
}

public interface IBinaryStatementOperation<TOperation> : IBinaryStatementOperation, IOperation<TOperation>
    where TOperation : IBinaryStatementOperation<TOperation>
{
    static readonly FunctionDeclaration OperationFunction = new(
        TOperation.Instance.Name,
        [
            new ParameterDeclaration("l", TOperation.Instance.LeftType, []),
            new ParameterDeclaration("r", TOperation.Instance.RightType, [])
        ],
        new FunctionReturn(UnitType.Instance, []),
        [new OperationMethodAttribute<TOperation>()]);

    FunctionDeclaration IOperation.Function => OperationFunction;
}
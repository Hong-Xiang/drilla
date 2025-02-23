using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Operation;

public interface IBinaryStatementOperation : IOperation
{
    public IShaderType LeftType { get; }
    public IShaderType RightType { get; }
    public IStatement CreateStatement(IExpression l, IExpression r);
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
    IStructuredStackInstruction IOperation.Instruction => BinaryStatementOperationInstruction<TOperation>.Instance;
}
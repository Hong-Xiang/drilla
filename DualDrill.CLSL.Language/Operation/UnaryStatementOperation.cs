using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Language.ValueInstruction;

namespace DualDrill.CLSL.Language.Operation;

public interface IUnaryStatementOperation : IOperation
{
    public IShaderType SourceType { get; }
    public IStatement CreateStatement(IExpression expression);
    
    public IStatementValueInstruction ToValueInstruction(IValue value);
}

public interface IUnaryStatementOperation<TOperation> : IUnaryStatementOperation, IOperation<TOperation>
    where TOperation : IUnaryStatementOperation<TOperation>
{
    static readonly FunctionDeclaration OperationFunction = new(
        TOperation.Instance.Name,
        [
            new ParameterDeclaration("value", TOperation.Instance.SourceType, [])
        ],
        new FunctionReturn(UnitType.Instance, []),
        [new OperationMethodAttribute<TOperation>()]);

    FunctionDeclaration IOperation.Function => OperationFunction;
    IInstruction IOperation.Instruction => UnaryStatementOperationInstruction<TOperation>.Instance;
}

public interface IUnaryStatementOperation<TOperation, TSourceType> : IUnaryStatementOperation<TOperation>
    where TOperation : IUnaryStatementOperation<TOperation, TSourceType>
    where TSourceType : ISingletonShaderType<TSourceType>
{
    IShaderType IUnaryStatementOperation.SourceType => TSourceType.Instance;
}
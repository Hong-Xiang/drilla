using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Value;

public interface IValue
{
}

public interface IValueInstruction
{
}

public interface IValue<TValueType>
    where TValueType : IShaderType<TValueType>
{
}

public sealed record class UnaryOperationValue<TOperation, TSource, TResult>(
    IValue<TSource> Oprand
) : IValue<TResult>
    where TOperation : IUnaryExpressionOperation
    where TSource : IShaderType<TSource>
    where TResult : IShaderType<TResult>
{
}

public sealed record class BinaryOperationValue<TOperation, TA, TB, TResult>(
    IValue<TA> FstOperand,
    IValue<TB> SndOperand
) : IValue<TResult>
    where TOperation : IBinaryExpressionOperation
    where TA : IShaderType<TA>
    where TB : IShaderType<TB>
    where TResult : IShaderType<TResult>
{
}


using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.LinearInstruction;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.Operation;

public interface IScalarConversionOperation
{
    IStructuredStackInstruction Instruction { get; }
}

public sealed class ScalarConversionOperation<TSource, TTarget>
    : IScalarConversionOperation, IUnaryScalarOperation<ScalarConversionOperation<TSource, TTarget>>
    , ISingleton<ScalarConversionOperation<TSource, TTarget>>
    where TSource : IScalarType<TSource>
    where TTarget : IScalarType<TTarget>
{
    public static ScalarConversionOperation<TSource, TTarget> Instance { get; } = new();
    public IStructuredStackInstruction Instruction => UnaryScalarInstruction<ScalarConversionOperation<TSource, TTarget>>.Instance;
}

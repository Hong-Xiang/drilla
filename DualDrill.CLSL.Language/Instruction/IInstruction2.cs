using DualDrill.CLSL.Language.Operation;

namespace DualDrill.CLSL.Language.Instruction;

public interface IInstruction2<T>
{
    IOperation Operation { get; }
    T? Result { get; }
    int OperandCount { get; }
    T this[int index] { get; }
}

public interface IInstructionSemantic<in TI, out TO>
{
    TO Nop();
    TO ScalarNumericArithmetic(IOperation operation, TI l, TI r);
}

using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Operation;

public sealed class StructuredBufferLengthOperation
    : IOperation<StructuredBufferLengthOperation>,
      IOperationRequirementProvider
{
    private StructuredBufferLengthOperation()
    {
        Function = new FunctionDeclaration(
            Name,
            [new ParameterDeclaration("buffer", BufferPointerType, [])],
            new FunctionReturn(ShaderType.U32, []),
            [new OperationMethodAttribute<StructuredBufferLengthOperation>()]);
    }

    public static StructuredBufferLengthOperation Instance { get; } = new();

    public IPtrType BufferPointerType { get; } =
        ReadOnlyStructuredBufferType.Instance.GetPtrType(StorageAddressSpace.Instance);

    public FunctionDeclaration Function { get; }
    public string Name => "structured-buffer-length-f32";
    public OperationRequirement Requirements => OperationRequirement.None;

    public IOperationMethodAttribute GetOperationMethodAttribute() =>
        new OperationMethodAttribute<StructuredBufferLengthOperation>();

    public TO EvaluateInstruction<TV, TR, TS, TO>(Instruction<TV, TR> instruction, TS semantic)
        where TS : IOperationSemantic<Instruction<TV, TR>, TV, TR, TO> =>
        semantic.StructuredBufferLength(instruction, this, instruction.Result, instruction[0]);
}

public sealed class StructuredBufferLoadOperation
    : IOperation<StructuredBufferLoadOperation>,
      IOperationRequirementProvider
{
    private StructuredBufferLoadOperation()
    {
        Function = new FunctionDeclaration(
            Name,
            [
                new ParameterDeclaration("buffer", BufferPointerType, []),
                new ParameterDeclaration("index", ShaderType.U32, [])
            ],
            new FunctionReturn(ShaderType.F32, []),
            [new OperationMethodAttribute<StructuredBufferLoadOperation>()]);
    }

    public static StructuredBufferLoadOperation Instance { get; } = new();

    public IPtrType BufferPointerType { get; } =
        ReadOnlyStructuredBufferType.Instance.GetPtrType(StorageAddressSpace.Instance);

    public FunctionDeclaration Function { get; }
    public string Name => "structured-buffer-load-f32";
    public OperationRequirement Requirements => OperationRequirement.MemoryRead;

    public IOperationMethodAttribute GetOperationMethodAttribute() =>
        new OperationMethodAttribute<StructuredBufferLoadOperation>();

    public TO EvaluateInstruction<TV, TR, TS, TO>(Instruction<TV, TR> instruction, TS semantic)
        where TS : IOperationSemantic<Instruction<TV, TR>, TV, TR, TO> =>
        semantic.StructuredBufferLoad(
            instruction,
            this,
            instruction.Result,
            instruction[0],
            instruction[1]);
}

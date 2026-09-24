using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Operation;

public interface IReadOnlyStructuredBufferLengthOperation : IOperation
{
    IPtrType BufferPointerType { get; }
}

public interface IReadOnlyStructuredBufferLoadOperation : IOperation
{
    IPtrType BufferPointerType { get; }
    IShaderType ElementType { get; }
}

public sealed class StructuredBufferLengthOperation<TElement>
    : IOperation<StructuredBufferLengthOperation<TElement>>,
      IReadOnlyStructuredBufferLengthOperation,
      IOperationRequirementProvider
    where TElement : IScalarType<TElement>
{
    private StructuredBufferLengthOperation()
    {
        Function = new FunctionDeclaration(
            Name,
            [new ParameterDeclaration("buffer", BufferPointerType, [])],
            new FunctionReturn(ShaderType.U32, []),
            [new OperationMethodAttribute<StructuredBufferLengthOperation<TElement>>()]);
    }

    public static StructuredBufferLengthOperation<TElement> Instance
    {
        get
        {
            ReadOnlyStructuredBufferFamily.RequireElement<TElement>();
            return Holder.Instance;
        }
    }

    private static class Holder
    {
        internal static readonly StructuredBufferLengthOperation<TElement> Instance = new();
    }

    public IPtrType BufferPointerType { get; } =
        ReadOnlyStructuredBufferType<TElement>.Instance.GetPtrType(StorageAddressSpace.Instance);

    public FunctionDeclaration Function { get; }
    public string Name => $"structured-buffer-length-{TElement.Instance.Name}";
    public OperationRequirement Requirements => OperationRequirement.None;

    public IOperationMethodAttribute GetOperationMethodAttribute() =>
        new OperationMethodAttribute<StructuredBufferLengthOperation<TElement>>();

    public TO EvaluateInstruction<TV, TR, TS, TO>(Instruction<TV, TR> instruction, TS semantic)
        where TS : IOperationSemantic<Instruction<TV, TR>, TV, TR, TO> =>
        semantic.StructuredBufferLength(instruction, this, instruction.Result, instruction[0]);
}

public sealed class StructuredBufferLoadOperation<TElement>
    : IOperation<StructuredBufferLoadOperation<TElement>>,
      IReadOnlyStructuredBufferLoadOperation,
      IOperationRequirementProvider
    where TElement : IScalarType<TElement>
{
    private StructuredBufferLoadOperation()
    {
        Function = new FunctionDeclaration(
            Name,
            [
                new ParameterDeclaration("buffer", BufferPointerType, []),
                new ParameterDeclaration("index", ShaderType.U32, [])
            ],
            new FunctionReturn(TElement.Instance, []),
            [new OperationMethodAttribute<StructuredBufferLoadOperation<TElement>>()]);
    }

    public static StructuredBufferLoadOperation<TElement> Instance
    {
        get
        {
            ReadOnlyStructuredBufferFamily.RequireElement<TElement>();
            return Holder.Instance;
        }
    }

    private static class Holder
    {
        internal static readonly StructuredBufferLoadOperation<TElement> Instance = new();
    }

    public IPtrType BufferPointerType { get; } =
        ReadOnlyStructuredBufferType<TElement>.Instance.GetPtrType(StorageAddressSpace.Instance);

    public FunctionDeclaration Function { get; }
    public IShaderType ElementType => TElement.Instance;
    public string Name => $"structured-buffer-load-{TElement.Instance.Name}";
    public OperationRequirement Requirements => OperationRequirement.MemoryRead;

    public IOperationMethodAttribute GetOperationMethodAttribute() =>
        new OperationMethodAttribute<StructuredBufferLoadOperation<TElement>>();

    public TO EvaluateInstruction<TV, TR, TS, TO>(Instruction<TV, TR> instruction, TS semantic)
        where TS : IOperationSemantic<Instruction<TV, TR>, TV, TR, TO> =>
        semantic.StructuredBufferLoad(
            instruction,
            this,
            instruction.Result,
            instruction[0],
            instruction[1]);
}

public sealed class ReadWriteStructuredBufferLengthOperation
    : IOperation<ReadWriteStructuredBufferLengthOperation>,
      IOperationRequirementProvider
{
    private ReadWriteStructuredBufferLengthOperation()
    {
        Function = new FunctionDeclaration(
            Name,
            [new ParameterDeclaration("buffer", BufferPointerType, [])],
            new FunctionReturn(ShaderType.U32, []),
            [new OperationMethodAttribute<ReadWriteStructuredBufferLengthOperation>()]);
    }

    public static ReadWriteStructuredBufferLengthOperation Instance { get; } = new();

    public IPtrType BufferPointerType { get; } =
        ReadWriteStructuredBufferType.Instance.GetPtrType(StorageAddressSpace.Instance);

    public FunctionDeclaration Function { get; }
    public string Name => "rw-structured-buffer-length-f32";
    public OperationRequirement Requirements => OperationRequirement.None;

    public IOperationMethodAttribute GetOperationMethodAttribute() =>
        new OperationMethodAttribute<ReadWriteStructuredBufferLengthOperation>();

    public TO EvaluateInstruction<TV, TR, TS, TO>(Instruction<TV, TR> instruction, TS semantic)
        where TS : IOperationSemantic<Instruction<TV, TR>, TV, TR, TO> =>
        semantic.ReadWriteStructuredBufferLength(instruction, this, instruction.Result, instruction[0]);
}

public sealed class ReadWriteStructuredBufferLoadOperation
    : IOperation<ReadWriteStructuredBufferLoadOperation>,
      IOperationRequirementProvider
{
    private ReadWriteStructuredBufferLoadOperation()
    {
        Function = new FunctionDeclaration(
            Name,
            [
                new ParameterDeclaration("buffer", BufferPointerType, []),
                new ParameterDeclaration("index", ShaderType.U32, [])
            ],
            new FunctionReturn(ShaderType.F32, []),
            [new OperationMethodAttribute<ReadWriteStructuredBufferLoadOperation>()]);
    }

    public static ReadWriteStructuredBufferLoadOperation Instance { get; } = new();

    public IPtrType BufferPointerType { get; } =
        ReadWriteStructuredBufferType.Instance.GetPtrType(StorageAddressSpace.Instance);

    public FunctionDeclaration Function { get; }
    public string Name => "rw-structured-buffer-load-f32";
    public OperationRequirement Requirements => OperationRequirement.MemoryRead;

    public IOperationMethodAttribute GetOperationMethodAttribute() =>
        new OperationMethodAttribute<ReadWriteStructuredBufferLoadOperation>();

    public TO EvaluateInstruction<TV, TR, TS, TO>(Instruction<TV, TR> instruction, TS semantic)
        where TS : IOperationSemantic<Instruction<TV, TR>, TV, TR, TO> =>
        semantic.ReadWriteStructuredBufferLoad(
            instruction,
            this,
            instruction.Result,
            instruction[0],
            instruction[1]);
}

public sealed class ReadWriteStructuredBufferStoreOperation
    : IOperation<ReadWriteStructuredBufferStoreOperation>,
      IOperationRequirementProvider
{
    private ReadWriteStructuredBufferStoreOperation()
    {
        Function = new FunctionDeclaration(
            Name,
            [
                new ParameterDeclaration("buffer", BufferPointerType, []),
                new ParameterDeclaration("index", ShaderType.U32, []),
                new ParameterDeclaration("value", ShaderType.F32, [])
            ],
            new FunctionReturn(UnitType.Instance, []),
            [new OperationMethodAttribute<ReadWriteStructuredBufferStoreOperation>()]);
    }

    public static ReadWriteStructuredBufferStoreOperation Instance { get; } = new();

    public IPtrType BufferPointerType { get; } =
        ReadWriteStructuredBufferType.Instance.GetPtrType(StorageAddressSpace.Instance);

    public FunctionDeclaration Function { get; }
    public string Name => "rw-structured-buffer-store-f32";
    public OperationRequirement Requirements => OperationRequirement.MemoryWrite;

    public IOperationMethodAttribute GetOperationMethodAttribute() =>
        new OperationMethodAttribute<ReadWriteStructuredBufferStoreOperation>();

    public TO EvaluateInstruction<TV, TR, TS, TO>(Instruction<TV, TR> instruction, TS semantic)
        where TS : IOperationSemantic<Instruction<TV, TR>, TV, TR, TO> =>
        semantic.ReadWriteStructuredBufferStore(
            instruction,
            this,
            instruction[0],
            instruction[1],
            instruction[2]);
}

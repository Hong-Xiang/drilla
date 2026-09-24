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
            StructuredBufferScalarFamily.RequireElement<TElement>();
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
            StructuredBufferScalarFamily.RequireElement<TElement>();
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

public interface IReadWriteStructuredBufferLengthOperation : IOperation
{
    IPtrType BufferPointerType { get; }
}

public interface IReadWriteStructuredBufferLoadOperation : IOperation
{
    IPtrType BufferPointerType { get; }
    IShaderType ElementType { get; }
}

public interface IReadWriteStructuredBufferStoreOperation : IOperation
{
    IPtrType BufferPointerType { get; }
    IShaderType ElementType { get; }
}

public sealed class ReadWriteStructuredBufferLengthOperation<TElement>
    : IOperation<ReadWriteStructuredBufferLengthOperation<TElement>>,
      IReadWriteStructuredBufferLengthOperation,
      IOperationRequirementProvider
    where TElement : IScalarType<TElement>
{
    private ReadWriteStructuredBufferLengthOperation()
    {
        Function = new FunctionDeclaration(
            Name,
            [new ParameterDeclaration("buffer", BufferPointerType, [])],
            new FunctionReturn(ShaderType.U32, []),
            [new OperationMethodAttribute<ReadWriteStructuredBufferLengthOperation<TElement>>()]);
    }

    public static ReadWriteStructuredBufferLengthOperation<TElement> Instance
    {
        get
        {
            StructuredBufferScalarFamily.RequireElement<TElement>();
            return Holder.Instance;
        }
    }

    private static class Holder
    {
        internal static readonly ReadWriteStructuredBufferLengthOperation<TElement> Instance = new();
    }

    public IPtrType BufferPointerType { get; } =
        ReadWriteStructuredBufferType<TElement>.Instance.GetPtrType(StorageAddressSpace.Instance);

    public FunctionDeclaration Function { get; }
    public string Name => $"rw-structured-buffer-length-{TElement.Instance.Name}";
    public OperationRequirement Requirements => OperationRequirement.None;

    public IOperationMethodAttribute GetOperationMethodAttribute() =>
        new OperationMethodAttribute<ReadWriteStructuredBufferLengthOperation<TElement>>();

    public TO EvaluateInstruction<TV, TR, TS, TO>(Instruction<TV, TR> instruction, TS semantic)
        where TS : IOperationSemantic<Instruction<TV, TR>, TV, TR, TO> =>
        semantic.ReadWriteStructuredBufferLength(instruction, this, instruction.Result, instruction[0]);
}

public sealed class ReadWriteStructuredBufferLoadOperation<TElement>
    : IOperation<ReadWriteStructuredBufferLoadOperation<TElement>>,
      IReadWriteStructuredBufferLoadOperation,
      IOperationRequirementProvider
    where TElement : IScalarType<TElement>
{
    private ReadWriteStructuredBufferLoadOperation()
    {
        Function = new FunctionDeclaration(
            Name,
            [
                new ParameterDeclaration("buffer", BufferPointerType, []),
                new ParameterDeclaration("index", ShaderType.U32, [])
            ],
            new FunctionReturn(TElement.Instance, []),
            [new OperationMethodAttribute<ReadWriteStructuredBufferLoadOperation<TElement>>()]);
    }

    public static ReadWriteStructuredBufferLoadOperation<TElement> Instance
    {
        get
        {
            StructuredBufferScalarFamily.RequireElement<TElement>();
            return Holder.Instance;
        }
    }

    private static class Holder
    {
        internal static readonly ReadWriteStructuredBufferLoadOperation<TElement> Instance = new();
    }

    public IPtrType BufferPointerType { get; } =
        ReadWriteStructuredBufferType<TElement>.Instance.GetPtrType(StorageAddressSpace.Instance);

    public FunctionDeclaration Function { get; }
    public IShaderType ElementType => TElement.Instance;
    public string Name => $"rw-structured-buffer-load-{TElement.Instance.Name}";
    public OperationRequirement Requirements => OperationRequirement.MemoryRead;

    public IOperationMethodAttribute GetOperationMethodAttribute() =>
        new OperationMethodAttribute<ReadWriteStructuredBufferLoadOperation<TElement>>();

    public TO EvaluateInstruction<TV, TR, TS, TO>(Instruction<TV, TR> instruction, TS semantic)
        where TS : IOperationSemantic<Instruction<TV, TR>, TV, TR, TO> =>
        semantic.ReadWriteStructuredBufferLoad(
            instruction,
            this,
            instruction.Result,
            instruction[0],
            instruction[1]);
}

public sealed class ReadWriteStructuredBufferStoreOperation<TElement>
    : IOperation<ReadWriteStructuredBufferStoreOperation<TElement>>,
      IReadWriteStructuredBufferStoreOperation,
      IOperationRequirementProvider
    where TElement : IScalarType<TElement>
{
    private ReadWriteStructuredBufferStoreOperation()
    {
        Function = new FunctionDeclaration(
            Name,
            [
                new ParameterDeclaration("buffer", BufferPointerType, []),
                new ParameterDeclaration("index", ShaderType.U32, []),
                new ParameterDeclaration("value", TElement.Instance, [])
            ],
            new FunctionReturn(UnitType.Instance, []),
            [new OperationMethodAttribute<ReadWriteStructuredBufferStoreOperation<TElement>>()]);
    }

    public static ReadWriteStructuredBufferStoreOperation<TElement> Instance
    {
        get
        {
            StructuredBufferScalarFamily.RequireElement<TElement>();
            return Holder.Instance;
        }
    }

    private static class Holder
    {
        internal static readonly ReadWriteStructuredBufferStoreOperation<TElement> Instance = new();
    }

    public IPtrType BufferPointerType { get; } =
        ReadWriteStructuredBufferType<TElement>.Instance.GetPtrType(StorageAddressSpace.Instance);

    public FunctionDeclaration Function { get; }
    public IShaderType ElementType => TElement.Instance;
    public string Name => $"rw-structured-buffer-store-{TElement.Instance.Name}";
    public OperationRequirement Requirements => OperationRequirement.MemoryWrite;

    public IOperationMethodAttribute GetOperationMethodAttribute() =>
        new OperationMethodAttribute<ReadWriteStructuredBufferStoreOperation<TElement>>();

    public TO EvaluateInstruction<TV, TR, TS, TO>(Instruction<TV, TR> instruction, TS semantic)
        where TS : IOperationSemantic<Instruction<TV, TR>, TV, TR, TO> =>
        semantic.ReadWriteStructuredBufferStore(
            instruction,
            this,
            instruction[0],
            instruction[1],
            instruction[2]);
}

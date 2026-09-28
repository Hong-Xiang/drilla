using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.Instruction;

public readonly record struct Instruction<TV, TR>(
    IOperation Operation,
    int OperandCount,
    TR? Result,
    TV? Operand0,
    TV? Operand1,
    ImmutableArray<TV> RestOperands,
    object? Payload
)
{
    public TR RequireResult() => Result is { } result
        ? result
        : throw new ArgumentException($"{Operation} requires a result.");

    public TV this[int index]
    {
        get
        {
            if (index < 0 || index >= OperandCount)
                throw new IndexOutOfRangeException(
                    $"Accessing {index} operand while instruction has {OperandCount} operands");
            ValidateOperandLayout();
            return (index switch
            {
                0 => Operand0,
                1 => Operand1,
                _ when !RestOperands.IsDefault && index - 2 < RestOperands.Length => RestOperands[index - 2],
                _ => default
            }) is { } value
                ? value
                : throw new ArgumentException($"{Operation} is missing operand {index}.");
        }
    }

    public IEnumerable<TV> Operands
    {
        get
        {
            ValidateOperandLayout();
            var instruction = this;
            return Enumerable.Range(0, OperandCount).Select(index => instruction[index]).ToImmutableArray();
        }
    }

    public bool HasValidOperandLayout =>
        OperandCount >= 0 &&
        (OperandCount == 0 ? Operand0 is null && Operand1 is null :
         OperandCount == 1 ? Operand0 is not null && Operand1 is null :
         Operand0 is not null && Operand1 is not null) &&
        !RestOperands.IsDefault &&
        RestOperands.Length == Math.Max(0, OperandCount - 2) &&
        RestOperands.All(static operand => operand is not null);

    private void ValidateOperandLayout()
    {
        if (!HasValidOperandLayout)
            throw new ArgumentException($"{Operation} has an invalid operand layout.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Evaluate<T>(IOperationSemantic<Instruction<TV, TR>, TV, TR, T> semantic) =>
        Evaluate<IOperationSemantic<Instruction<TV, TR>, TV, TR, T>, T>(semantic);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Evaluate<TS, T>(TS semantic)
        where TS : IOperationSemantic<Instruction<TV, TR>, TV, TR, T> =>
        Operation.EvaluateInstruction<
            TV,
            TR,
            TS,
            T>(this, semantic);

    public Instruction<TVR, TRR> Select<TVR, TRR>(Func<TV, TVR> fu, Func<TR, TRR> fd) =>
        Instruction<TVR, TRR>.Create(
            Operation, Result is { } result ? fd(result) : default, Operands.Select(fu), Payload);

    public static Instruction<TV, TR> Create(IOperation op, TR? result, IEnumerable<TV> operands,
        object? payload = null)
    {
        var ops = operands.ToImmutableArray();
        if (ops.Any(static operand => operand is null))
            throw new ArgumentException("Instruction operands cannot be null.", nameof(operands));
        return ops.Length switch
        {
            0 => new Instruction<TV, TR>(op, 0, result, default, default, [], payload),
            1 => new Instruction<TV, TR>(op, 1, result, ops[0], default, [], payload),
            2 => new Instruction<TV, TR>(op, 2, result, ops[0], ops[1], [], payload),
            _ => new Instruction<TV, TR>(op, ops.Length, result, ops[0], ops[1], ops[2..], payload)
        };
    }
}

public static class Instruction
{
    public static IOperationSemantic<Unit, IShaderValue, IShaderValue, Instruction<IShaderValue, IShaderValue>> Factory
    {
        get;
    } = new OperationInstructionFactory();

    private sealed class OperationInstructionFactory : IOperationSemantic<Unit, IShaderValue, IShaderValue,
        Instruction<IShaderValue, IShaderValue>>
    {
        public Instruction<IShaderValue, IShaderValue> AddressOfChain(Unit ctx, IAddressOfOperation op,
            IShaderValue result, IShaderValue target) =>
            Create(op, result, [target]);

        public Instruction<IShaderValue, IShaderValue> AddressOfChain(Unit ctx, IAddressOfOperation op,
            IShaderValue result, IShaderValue target, IShaderValue index) =>
            Create(op, result, [target, index]);

        public Instruction<IShaderValue, IShaderValue> Call(Unit ctx, CallOperation op, IShaderValue? result,
            IShaderValue f, IReadOnlyList<IShaderValue> arguments) =>
            Create(op, result, [f, .. arguments]);

        public Instruction<IShaderValue, IShaderValue> Literal(Unit ctx, LiteralOperation op, IShaderValue result,
            IShaderValue value) =>
            Create(op, result, [value]);

        public Instruction<IShaderValue, IShaderValue> Load(Unit ctx, LoadOperation op, IShaderValue result,
            IShaderValue ptr) =>
            Create(op, result, [ptr]);

        public Instruction<IShaderValue, IShaderValue> Nop(Unit ctx, NopOperation op) => Create(op, default, []);

        public Instruction<IShaderValue, IShaderValue> Operation1(Unit ctx, IUnaryExpressionOperation op,
            IShaderValue result, IShaderValue e) =>
            Create(op, result, [e]);

        public Instruction<IShaderValue, IShaderValue> Operation2(Unit ctx, IBinaryExpressionOperation op,
            IShaderValue result, IShaderValue l, IShaderValue r) =>
            Create(op, result, [l, r]);

        public Instruction<IShaderValue, IShaderValue> StructuredBufferLength(
            Unit ctx,
            IReadOnlyStructuredBufferLengthOperation op,
            IShaderValue result,
            IShaderValue buffer) =>
            Create(op, result, [buffer]);

        public Instruction<IShaderValue, IShaderValue> StructuredBufferLoad(
            Unit ctx,
            IReadOnlyStructuredBufferLoadOperation op,
            IShaderValue result,
            IShaderValue buffer,
            IShaderValue index) =>
            Create(op, result, [buffer, index]);

        public Instruction<IShaderValue, IShaderValue> ReadWriteStructuredBufferLength(
            Unit ctx,
            IReadWriteStructuredBufferLengthOperation op,
            IShaderValue result,
            IShaderValue buffer) =>
            Create(op, result, [buffer]);

        public Instruction<IShaderValue, IShaderValue> ReadWriteStructuredBufferLoad(
            Unit ctx,
            IReadWriteStructuredBufferLoadOperation op,
            IShaderValue result,
            IShaderValue buffer,
            IShaderValue index) =>
            Create(op, result, [buffer, index]);

        public Instruction<IShaderValue, IShaderValue> ReadWriteStructuredBufferStore(
            Unit ctx,
            IReadWriteStructuredBufferStoreOperation op,
            IShaderValue buffer,
            IShaderValue index,
            IShaderValue value) =>
            Create(op, default, [buffer, index, value]);

        public Instruction<IShaderValue, IShaderValue> TextureSampleLevel(
            Unit ctx,
            TextureSampleLevelOperation op,
            IShaderValue result,
            IShaderValue texture,
            IShaderValue sampler,
            IShaderValue uv,
            IShaderValue lod) =>
            Create(op, result, [texture, sampler, uv, lod]);

        public Instruction<IShaderValue, IShaderValue> Store(Unit ctx, StoreOperation op, IShaderValue ptr,
            IShaderValue value) =>
            Create(op, default, [ptr, value]);

        public Instruction<IShaderValue, IShaderValue> VectorComponentSet(Unit ctx, IVectorComponentSetOperation op,
            IShaderValue ptr, IShaderValue value) =>
            Create(op, default, [ptr, value]);

        public Instruction<IShaderValue, IShaderValue> VectorCompositeConstruction(Unit ctx,
            VectorCompositeConstructionOperation op, IShaderValue result, IReadOnlyList<IShaderValue> components) =>
            Create(op, result, components);

        public Instruction<IShaderValue, IShaderValue> StructureCompositeConstruction(Unit ctx,
            StructureCompositeConstructionOperation op, IShaderValue result, IReadOnlyList<IShaderValue> members) =>
            Create(op, result, members);


        public Instruction<IShaderValue, IShaderValue> VectorSwizzleSet(Unit ctx, IVectorSwizzleSetOperation op,
            IShaderValue ptr, IShaderValue value) =>
            Create(op, default, [ptr, value]);

        private static Instruction<IShaderValue, IShaderValue> Create(IOperation op, IShaderValue? result,
            IEnumerable<IShaderValue> operands, object? payload = null) =>
            Instruction<IShaderValue, IShaderValue>.Create(op, result, operands, payload);

        public Instruction<IShaderValue, IShaderValue> AccessChain(Unit ctx, AccessChainOperation op, IShaderValue result, IShaderValue target, IReadOnlyList<IShaderValue> indices)
            => Create(op, result, [target, .. indices]);

        public Instruction<IShaderValue, IShaderValue> ZeroConstructorOperation(Unit ctx, ZeroConstructorOperation op, IShaderValue result)
            => Create(op, result, []);
    }
}
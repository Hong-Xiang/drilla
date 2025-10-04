﻿using System.Collections.Immutable;
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
    public TV? this[int index] =>
        index < OperandCount
            ? index switch
            {
                0 => Operand0,
                1 => Operand1,
                _ => index - 2 < RestOperands.Length ? RestOperands[index - 2] : default
            }
            : throw new IndexOutOfRangeException(
                $"Accessing {index} operand while instruction has {OperandCount} operands");

    public IEnumerable<TV> Operands =>
        OperandCount switch
        {
            0 => [],
            1 => [Operand0!],
            2 => [Operand0!, Operand1!],
            _ => [Operand0!, Operand1!, .. RestOperands]
        };

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
        new(Operation,
            OperandCount,
            Result is not null ? fd(Result) : default,
            Operand0 is not null ? fu(Operand0) : default,
            Operand1 is not null ? fu(Operand1) : default,
            [.. RestOperands.Select(fu)],
            Payload
        );

    public static Instruction<TV, TR> Create(IOperation op, TR? result, IEnumerable<TV> operands,
        object? payload = null)
    {
        var ops = operands.ToImmutableArray();
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

        public Instruction<IShaderValue, IShaderValue> Call(Unit ctx, CallOperation op, IShaderValue result,
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

        public Instruction<IShaderValue, IShaderValue> Store(Unit ctx, StoreOperation op, IShaderValue ptr,
            IShaderValue value) =>
            Create(op, default, [ptr, value]);

        public Instruction<IShaderValue, IShaderValue> VectorComponentSet(Unit ctx, IVectorComponentSetOperation op,
            IShaderValue ptr, IShaderValue value) =>
            Create(op, default, [ptr, value]);

        public Instruction<IShaderValue, IShaderValue> VectorCompositeConstruction(Unit ctx,
            VectorCompositeConstructionOperation op, IShaderValue result, IReadOnlyList<IShaderValue> components) =>
            Create(op, result, components);


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
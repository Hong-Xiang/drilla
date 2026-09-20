using System.Collections.Immutable;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Test;

// Test-only scalar semantics, not a CIL interpreter or a GPU/reconvergence oracle.
internal static class ScalarControlFlowOracle
{
    internal abstract record Value
    {
        private Value() { }
        internal sealed record Integer(int Data) : Value;
        internal sealed record LongInteger(long Data) : Value;
        internal sealed record UnsignedInteger(uint Data) : Value;
        internal sealed record Float32(float Data) : Value;
        internal sealed record Float64(double Data) : Value;
        internal sealed record Boolean(bool Data) : Value;
        internal sealed record Address(IShaderValue Storage) : Value;

        internal int Int => this is Integer i ? i.Data : throw new NotSupportedException($"Expected i32, got {this}");
        internal long Long => this is LongInteger i
            ? i.Data
            : throw new NotSupportedException($"Expected i64, got {this}");
        internal uint UInt => this is UnsignedInteger i
            ? i.Data
            : throw new NotSupportedException($"Expected u32, got {this}");
        internal float Single => this is Float32 f
            ? f.Data
            : throw new NotSupportedException($"Expected f32, got {this}");
        internal double Double => this is Float64 f
            ? f.Data
            : throw new NotSupportedException($"Expected f64, got {this}");
        internal bool Bool => this is Boolean b ? b.Data : throw new NotSupportedException($"Expected bool, got {this}");
    }

    internal sealed record Execution(Value Result, ImmutableArray<Label> Trace);

    internal sealed class Budget(string context, int limit)
    {
        private int steps;
        internal void Step(string location)
        {
            if (++steps > limit)
                throw new InvalidOperationException($"{context}: step budget {limit} exhausted at {location}.");
        }
    }

    internal abstract record Control
    {
        private Control() { }
        internal sealed record Returned(Value Value) : Control;
        internal sealed record Transfer(RegionJump<IShaderValue> Jump) : Control;
    }

    internal sealed class Machine
    {
        private readonly string context;
        private readonly Budget budget;
        private readonly IShaderType? declaredReturnType;
        private readonly Dictionary<IShaderValue, Value> values =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<IShaderValue, Value> memory;
        private readonly ImmutableArray<Label>.Builder trace = ImmutableArray.CreateBuilder<Label>();

        internal Machine(RegionFunctionBody body, ImmutableArray<Value> arguments, string kind, int stepLimit)
            : this(body.Declaration, arguments, [], false, kind, stepLimit)
        {
        }

        private Machine(
            FunctionDeclaration declaration,
            ImmutableArray<Value> arguments,
            ImmutableArray<VariableDeclaration> locals,
            bool initLocals,
            string kind,
            int stepLimit)
        {
            context = $"{kind} {declaration.Name}";
            if (!IsScalar(declaration.ReturnType) || declaration.Parameters.Any(p => !IsScalar(p.Type)))
                throw new NotSupportedException($"{context}: unsupported scalar function signature.");
            if (arguments.Length != declaration.Parameters.Length)
                throw new InvalidOperationException($"{context}: incorrect argument count.");

            budget = new Budget(context, stepLimit);
            declaredReturnType = declaration.ReturnType;
            memory = new Dictionary<IShaderValue, Value>(ReferenceEqualityComparer.Instance);
            foreach (var (parameter, argument) in declaration.Parameters.Zip(arguments))
                Write(memory, parameter.Value, argument, parameter.Type);
            if (initLocals)
                foreach (var local in locals)
                    memory.Add(local.Value, Zero(local.Type, context));
        }

        private Machine(
            string context,
            Dictionary<IShaderValue, Value> memory,
            IShaderType? declaredReturnType,
            int stepLimit)
        {
            this.context = context;
            budget = new Budget(context, stepLimit);
            this.memory = memory;
            this.declaredReturnType = declaredReturnType;
        }

        internal static Machine ForFlatCfg(
            ImmutableArray<VariableDeclaration> locals,
            bool initLocals,
            int stepLimit)
        {
            var memory = new Dictionary<IShaderValue, Value>(ReferenceEqualityComparer.Instance);
            if (initLocals)
                foreach (var local in locals)
                    memory.Add(local.Value, Zero(local.Type, "Flat CFG"));
            return new Machine("Flat CFG", memory, null, stepLimit);
        }

        internal static Machine ForValueCfg(
            FunctionDeclaration declaration,
            ImmutableArray<Value> arguments,
            ImmutableArray<VariableDeclaration> locals,
            bool initLocals,
            string kind,
            int stepLimit) =>
            new(declaration, arguments, locals, initLocals, kind, stepLimit);

        internal Control Execute(Label label, ShaderRegionBody block)
            => Execute(label, block.Body);

        internal Control Execute(Label label, CilValueBasicBlock block)
            => Execute(label, block.Body);

        private Control Execute(
            Label label,
            Seq<Instruction<IShaderValue, IShaderValue>,
                ITerminator<RegionJump<IShaderValue>, IShaderValue>> body)
        {
            budget.Step(label.ToString());
            trace.Add(label);
            foreach (var instruction in body.Elements)
            {
                budget.Step($"{label}: {instruction.Operation.Name}");
                Value result;
                switch (instruction.Operation)
                {
                    case NopOperation:
                        continue;
                    case StoreOperation:
                        if (Read(instruction.Operand0) is not Value.Address store)
                            throw new NotSupportedException($"{context}, {label}: store needs a stable address.");
                        if (store.Storage.Type is not IPtrType pointer)
                            throw new NotSupportedException($"{context}, {label}: unsupported store type.");
                        Write(memory, store.Storage, Read(instruction.Operand1), pointer.BaseType);
                        continue;
                    case LoadOperation:
                        if (Read(instruction.Operand0) is not Value.Address load ||
                            !memory.TryGetValue(load.Storage, out var loaded))
                            throw new InvalidOperationException($"{context}, {label}: uninitialized or unsupported load.");
                        result = loaded;
                        break;
                    case LiteralOperation:
                        result = Read(instruction.Operand0);
                        break;
                    case IBinaryExpressionOperation binary:
                        var left = Read(instruction.Operand0);
                        var right = Read(instruction.Operand1);
                        if (!HasType(left, binary.LeftType) || !HasType(right, binary.RightType))
                            throw new NotSupportedException($"{context}, {label}: invalid binary operand types.");
                        result = Binary(binary.BinaryOp, left, right);
                        break;
                    case IConversionOperation conversion:
                        var source = Read(instruction.Operand0);
                        if (!IsScalar(conversion.SourceType) || !HasType(source, conversion.SourceType))
                            throw new NotSupportedException($"{context}, {label}: unsupported conversion {conversion.Name}.");
                        result = Convert(source, conversion.ResultType);
                        break;
                    case LogicalNotOperation:
                        result = new Value.Boolean(!Read(instruction.Operand0).Bool);
                        break;
                    case IUnaryExpressionOperation unary:
                        var operand = Read(instruction.Operand0);
                        if (!HasType(operand, unary.SourceType))
                            throw new NotSupportedException($"{context}, {label}: invalid unary operand type.");
                        result = Unary(unary, operand);
                        break;
                    default:
                        throw new NotSupportedException(
                            $"{context}, {label}: unsupported operation {instruction.Operation.Name}.");
                }
                var destination = instruction.Result ??
                    throw new InvalidOperationException($"{context}, {label}: missing instruction result.");
                Write(values, destination, result, destination.Type);
            }

            budget.Step($"{label}: terminator");
            return body.Last switch
            {
                Terminator.D.ReturnExpr<RegionJump<IShaderValue>, IShaderValue> returned =>
                    new Control.Returned(Read(returned.Expr)),
                Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue> branch =>
                    new Control.Transfer(branch.Target),
                Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue> branch =>
                    new Control.Transfer(Read(branch.Condition).Bool ? branch.TrueTarget : branch.FalseTarget),
                _ => throw new NotSupportedException($"{context}, {label}: unsupported terminator {body.Last}.")
            };
        }

        internal ImmutableArray<Value> ReadArguments(RegionJump<IShaderValue> jump) =>
            [.. jump.Arguments.Select(Read)];

        internal void Bind(ShaderRegionBody target, ImmutableArray<Value> incoming)
            => Bind(target.Label, target.Parameters, incoming);

        internal void Bind(CilValueBasicBlock target, ImmutableArray<Value> incoming)
            => Bind(target.Label, target.Parameters, incoming);

        private void Bind(
            Label label,
            ImmutableArray<IShaderValue> parameters,
            ImmutableArray<Value> incoming)
        {
            if (parameters.Length != incoming.Length)
                throw new InvalidOperationException($"{context}: invalid edge to {label} arity.");
            foreach (var (parameter, argument) in parameters.Zip(incoming))
                Write(values, parameter, argument, parameter.Type);
        }

        internal Execution Complete(Value result)
        {
            if (declaredReturnType is not null && !HasType(result, declaredReturnType))
            {
                var label = trace.Count == 0 ? "<no block>" : trace[trace.Count - 1].ToString();
                throw new NotSupportedException(
                    $"{context}, {label}: return value does not match {declaredReturnType.Name}.");
            }
            return new Execution(result, trace.ToImmutable());
        }

        private Value Read(IShaderValue? value) => value switch
        {
            LiteralValue { Value: I32Literal i } => new Value.Integer(i.Value),
            LiteralValue { Value: I64Literal i } => new Value.LongInteger(i.Value),
            LiteralValue { Value: U32Literal i } => new Value.UnsignedInteger(i.Value),
            LiteralValue { Value: F32Literal f } => new Value.Float32(f.Value),
            LiteralValue { Value: F64Literal f } => new Value.Float64(f.Value),
            LiteralValue { Value: BoolLiteral b } => new Value.Boolean(b.Value),
            ParameterPointerValue or VariablePointerValue => new Value.Address(value),
            not null when values.TryGetValue(value, out var result) => result,
            _ => throw new NotSupportedException($"{context}: unsupported or undefined value {value}.")
        };

        private void Write(Dictionary<IShaderValue, Value> target, IShaderValue key, Value value, IShaderType type)
        {
            if (!HasType(value, type))
                throw new NotSupportedException($"{context}: expected {type.Name}, got {value}.");
            target[key] = value;
        }
    }

    internal static Value Convert(Value value, IShaderType type) => (value, type) switch
    {
        (Value.Integer, BoolType) => new Value.Boolean(value.Int != 0),
        (Value.Boolean, var t) when t.Equals(ShaderType.I32) => new Value.Integer(value.Bool ? 1 : 0),
        (Value.Integer, var t) when t.Equals(ShaderType.U32) =>
            new Value.UnsignedInteger(unchecked((uint)value.Int)),
        (Value.UnsignedInteger, var t) when t.Equals(ShaderType.I32) =>
            new Value.Integer(unchecked((int)value.UInt)),
        _ when HasType(value, type) => value,
        _ => throw new NotSupportedException($"Unsupported scalar conversion {value} -> {type.Name}")
    };

    internal static bool HasType(Value value, IShaderType type) => value switch
    {
        Value.Integer => type.Equals(ShaderType.I32),
        Value.LongInteger => type.Equals(ShaderType.I64),
        Value.UnsignedInteger => type.Equals(ShaderType.U32),
        Value.Float32 => type.Equals(ShaderType.F32),
        Value.Float64 => type.Equals(ShaderType.F64),
        Value.Boolean => type.Equals(ShaderType.Bool),
        Value.Address a => a.Storage.Type.Equals(type),
        _ => throw new NotSupportedException($"Unsupported value {value}")
    };

    internal static Value Binary(IBinaryOp op, Value left, Value right)
    {
        return (left, right, op) switch
        {
            (Value.Integer l, Value.Integer r, BinaryRelational.Eq) => new Value.Boolean(l.Data == r.Data),
            (Value.Integer l, Value.Integer r, BinaryRelational.Ne) => new Value.Boolean(l.Data != r.Data),
            (Value.Integer l, Value.Integer r, BinaryRelational.Lt) => new Value.Boolean(l.Data < r.Data),
            (Value.Integer l, Value.Integer r, BinaryRelational.Le) => new Value.Boolean(l.Data <= r.Data),
            (Value.Integer l, Value.Integer r, BinaryRelational.Gt) => new Value.Boolean(l.Data > r.Data),
            (Value.Integer l, Value.Integer r, BinaryRelational.Ge) => new Value.Boolean(l.Data >= r.Data),
            (Value.Integer l, Value.Integer r, BinaryArithmetic.Add) =>
                new Value.Integer(unchecked(l.Data + r.Data)),
            (Value.Integer l, Value.Integer r, BinaryArithmetic.Sub) =>
                new Value.Integer(unchecked(l.Data - r.Data)),
            (Value.Integer l, Value.Integer r, BinaryArithmetic.Mul) =>
                new Value.Integer(unchecked(l.Data * r.Data)),
            (Value.UnsignedInteger l, Value.UnsignedInteger r, BinaryRelational.Eq) =>
                new Value.Boolean(l.Data == r.Data),
            (Value.UnsignedInteger l, Value.UnsignedInteger r, BinaryRelational.Ne) =>
                new Value.Boolean(l.Data != r.Data),
            (Value.UnsignedInteger l, Value.UnsignedInteger r, BinaryRelational.Lt) =>
                new Value.Boolean(l.Data < r.Data),
            (Value.UnsignedInteger l, Value.UnsignedInteger r, BinaryRelational.Le) =>
                new Value.Boolean(l.Data <= r.Data),
            (Value.UnsignedInteger l, Value.UnsignedInteger r, BinaryRelational.Gt) =>
                new Value.Boolean(l.Data > r.Data),
            (Value.UnsignedInteger l, Value.UnsignedInteger r, BinaryRelational.Ge) =>
                new Value.Boolean(l.Data >= r.Data),
            (Value.UnsignedInteger l, Value.UnsignedInteger r, BinaryArithmetic.Div) =>
                new Value.UnsignedInteger(l.Data / r.Data),
            (Value.UnsignedInteger l, Value.UnsignedInteger r, BinaryArithmetic.Rem) =>
                new Value.UnsignedInteger(l.Data % r.Data),
            (Value.Float32 l, Value.Float32 r, BinaryRelational.Eq) => new Value.Boolean(l.Data == r.Data),
            (Value.Float32 l, Value.Float32 r, BinaryRelational.Ne) => new Value.Boolean(l.Data != r.Data),
            (Value.Float32 l, Value.Float32 r, BinaryRelational.Lt) => new Value.Boolean(l.Data < r.Data),
            (Value.Float32 l, Value.Float32 r, BinaryRelational.Le) => new Value.Boolean(l.Data <= r.Data),
            (Value.Float32 l, Value.Float32 r, BinaryRelational.Gt) => new Value.Boolean(l.Data > r.Data),
            (Value.Float32 l, Value.Float32 r, BinaryRelational.Ge) => new Value.Boolean(l.Data >= r.Data),
            (Value.Float64 l, Value.Float64 r, BinaryRelational.Eq) => new Value.Boolean(l.Data == r.Data),
            (Value.Float64 l, Value.Float64 r, BinaryRelational.Ne) => new Value.Boolean(l.Data != r.Data),
            (Value.Float64 l, Value.Float64 r, BinaryRelational.Lt) => new Value.Boolean(l.Data < r.Data),
            (Value.Float64 l, Value.Float64 r, BinaryRelational.Le) => new Value.Boolean(l.Data <= r.Data),
            (Value.Float64 l, Value.Float64 r, BinaryRelational.Gt) => new Value.Boolean(l.Data > r.Data),
            (Value.Float64 l, Value.Float64 r, BinaryRelational.Ge) => new Value.Boolean(l.Data >= r.Data),
            (Value.Boolean l, Value.Boolean r, BinaryRelational.Eq) => new Value.Boolean(l.Data == r.Data),
            (Value.Boolean l, Value.Boolean r, BinaryRelational.Ne) => new Value.Boolean(l.Data != r.Data),
            _ => throw new NotSupportedException($"Unsupported scalar binary operation {op} for {left}, {right}.")
        };
    }

    internal static Value Unary(IUnaryExpressionOperation operation, Value operand) =>
        (operation, operand) switch
        {
            (UnaryNumericArithmeticExpressionOperation<IntType<DualDrill.Common.Nat.N32>,
                UnaryArithmetic.Negate>, Value.Integer value) =>
                new Value.Integer(unchecked(-value.Data)),
            (UnaryNumericArithmeticExpressionOperation<IntType<DualDrill.Common.Nat.N64>,
                UnaryArithmetic.Negate>, Value.LongInteger value) =>
                new Value.LongInteger(unchecked(-value.Data)),
            (UnaryNumericArithmeticExpressionOperation<FloatType<DualDrill.Common.Nat.N32>,
                UnaryArithmetic.Negate>, Value.Float32 value) =>
                new Value.Float32(-value.Data),
            (UnaryNumericArithmeticExpressionOperation<FloatType<DualDrill.Common.Nat.N64>,
                UnaryArithmetic.Negate>, Value.Float64 value) =>
                new Value.Float64(-value.Data),
            _ => throw new NotSupportedException(
                $"Unsupported scalar unary operation {operation.Name} for {operand}.")
        };

    internal static Execution RunCfg(RegionFunctionBody body, ImmutableArray<Value> arguments, int stepLimit = 10000)
    {
        var machine = new Machine(body, arguments, "CFG", stepLimit);
        var label = body.Entry;
        while (true)
        {
            var block = body[label];
            switch (machine.Execute(label, block))
            {
                case Control.Returned returned:
                    return machine.Complete(returned.Value);
                case Control.Transfer transfer:
                    var incoming = machine.ReadArguments(transfer.Jump);
                    var target = body[transfer.Jump.Label];
                    machine.Bind(target, incoming);
                    label = transfer.Jump.Label;
                    break;
            }
        }
    }

    internal static Execution RunValueCfg(
        CilValueControlFlowBody body,
        ImmutableArray<Value> arguments,
        int stepLimit = 10000)
    {
        var raw = body.Source.Source.Source.Raw;
        var methodBody = raw.Code.Environment.Body
                         ?? throw new InvalidOperationException(
                             $"Value CFG {raw.Code.Environment.Method} has no MethodBody metadata.");
        var machine = Machine.ForValueCfg(
            body.Declaration,
            arguments,
            raw.DeclarationContext.LocalVariables,
            methodBody.InitLocals,
            "value CFG",
            stepLimit);
        var label = body.Graph.EntryLabel;
        while (true)
        {
            var block = body.Graph[label];
            switch (machine.Execute(label, block))
            {
                case Control.Returned returned:
                    return machine.Complete(returned.Value);
                case Control.Transfer transfer:
                    var incoming = machine.ReadArguments(transfer.Jump);
                    var target = body.Graph[transfer.Jump.Label];
                    machine.Bind(target, incoming);
                    label = transfer.Jump.Label;
                    break;
            }
        }
    }

    internal static Execution RunFactsCfg(
        CilValueControlFactsBody body,
        ImmutableArray<Value> arguments,
        int stepLimit = 10000)
    {
        var raw = body.Source.Source.Source.Source.Raw;
        var methodBody = raw.Code.Environment.Body
                         ?? throw new InvalidOperationException(
                             $"Control-facts CFG {raw.Code.Environment.Method} has no MethodBody metadata.");
        var machine = Machine.ForValueCfg(
            body.Declaration,
            arguments,
            raw.DeclarationContext.LocalVariables,
            methodBody.InitLocals,
            "control-facts CFG",
            stepLimit);
        var label = body.Graph.EntryLabel;
        while (true)
        {
            var block = body.Graph[label].Node;
            switch (machine.Execute(label, block))
            {
                case Control.Returned returned:
                    return machine.Complete(returned.Value);
                case Control.Transfer transfer:
                    var incoming = machine.ReadArguments(transfer.Jump);
                    var target = body.Graph[transfer.Jump.Label].Node;
                    machine.Bind(target, incoming);
                    label = transfer.Jump.Label;
                    break;
            }
        }
    }

    internal static Execution RunCfg(
        ControlFlowGraph<CilValueBasicBlock> graph,
        ImmutableArray<VariableDeclaration> locals,
        bool initLocals,
        int stepLimit = 10000)
    {
        var machine = Machine.ForFlatCfg(locals, initLocals, stepLimit);
        var label = graph.EntryLabel;
        while (true)
        {
            var block = graph[label];
            switch (machine.Execute(label, block))
            {
                case Control.Returned returned:
                    return machine.Complete(returned.Value);
                case Control.Transfer transfer:
                    var incoming = machine.ReadArguments(transfer.Jump);
                    var target = graph[transfer.Jump.Label];
                    machine.Bind(target, incoming);
                    label = transfer.Jump.Label;
                    break;
            }
        }
    }

    private static Value Zero(IShaderType type, string context) => type switch
    {
        IntType<DualDrill.Common.Nat.N32> => new Value.Integer(0),
        IntType<DualDrill.Common.Nat.N64> => new Value.LongInteger(0),
        UIntType<DualDrill.Common.Nat.N32> => new Value.UnsignedInteger(0),
        FloatType<DualDrill.Common.Nat.N32> => new Value.Float32(0),
        FloatType<DualDrill.Common.Nat.N64> => new Value.Float64(0),
        BoolType => new Value.Boolean(false),
        _ => throw new NotSupportedException(
            $"{context}: unsupported initialized local type {type.Name}.")
    };

    internal static void AssertEquivalent(Execution expected, Execution actual)
    {
        Assert.True(expected.Trace.SequenceEqual(actual.Trace),
            $"Expected result: {expected.Result}; actual: {actual.Result}\n" +
            $"Original blocks: {string.Join(" -> ", expected.Trace)}\nExecuted blocks: {string.Join(" -> ", actual.Trace)}");
        Assert.Equal(expected.Result, actual.Result);
    }

    private static bool IsScalar(IShaderType type) =>
        type.Equals(ShaderType.I32) ||
        type.Equals(ShaderType.I64) ||
        type.Equals(ShaderType.U32) ||
        type.Equals(ShaderType.F32) ||
        type.Equals(ShaderType.F64) ||
        type.Equals(ShaderType.Bool);
}

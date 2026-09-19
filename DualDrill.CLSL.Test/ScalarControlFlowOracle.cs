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
        internal sealed record Boolean(bool Data) : Value;
        internal sealed record Address(IShaderValue Storage) : Value;

        internal int Int => this is Integer i ? i.Data : throw new NotSupportedException($"Expected i32, got {this}");
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
        private readonly Func<Value, Value> normalizeResult;
        private readonly Dictionary<IShaderValue, Value> values =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<IShaderValue, Value> memory;
        private readonly ImmutableArray<Label>.Builder trace = ImmutableArray.CreateBuilder<Label>();

        internal Machine(FunctionBody4 body, ImmutableArray<Value> arguments, string kind, int stepLimit)
        {
            context = $"{kind} {body.Declaration.Name}";
            if (!IsScalar(body.Declaration.ReturnType) || body.Declaration.Parameters.Any(p => !IsScalar(p.Type)))
                throw new NotSupportedException($"{context}: only i32/bool function signatures are supported.");
            if (arguments.Length != body.Declaration.Parameters.Length)
                throw new InvalidOperationException($"{context}: incorrect argument count.");

            budget = new Budget(context, stepLimit);
            normalizeResult = result => Convert(result, body.Declaration.ReturnType);
            memory = new Dictionary<IShaderValue, Value>(ReferenceEqualityComparer.Instance);
            foreach (var (parameter, argument) in body.Declaration.Parameters.Zip(arguments))
                Write(memory, parameter.Value, argument, parameter.Type);
        }

        private Machine(
            string context,
            Dictionary<IShaderValue, Value> memory,
            Func<Value, Value> normalizeResult,
            int stepLimit)
        {
            this.context = context;
            budget = new Budget(context, stepLimit);
            this.memory = memory;
            this.normalizeResult = normalizeResult;
        }

        internal static Machine ForFlatCfg(
            ImmutableArray<VariableDeclaration> locals,
            bool initLocals,
            int stepLimit)
        {
            var memory = new Dictionary<IShaderValue, Value>(ReferenceEqualityComparer.Instance);
            if (initLocals)
                foreach (var local in locals)
                    memory.Add(local.Value, local.Type switch
                    {
                        IntType<DualDrill.Common.Nat.N32> => new Value.Integer(0),
                        BoolType => new Value.Boolean(false),
                        _ => throw new NotSupportedException(
                            $"Flat CFG: unsupported initialized local type {local.Type.Name}.")
                    });
            return new Machine("Flat CFG", memory, result => result, stepLimit);
        }

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
                        if (!IsScalar(conversion.SourceType))
                            throw new NotSupportedException($"{context}, {label}: unsupported conversion {conversion.Name}.");
                        result = Convert(Read(instruction.Operand0), conversion.ResultType);
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

        internal Execution Complete(Value result) =>
            new(normalizeResult(result), trace.ToImmutable());

        private Value Read(IShaderValue? value) => value switch
        {
            LiteralValue { Value: I32Literal i } => new Value.Integer(i.Value),
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
        _ when HasType(value, type) => value,
        _ => throw new NotSupportedException($"Unsupported scalar conversion {value} -> {type.Name}")
    };

    internal static bool HasType(Value value, IShaderType type) => value switch
    {
        Value.Integer => type.Equals(ShaderType.I32),
        Value.Boolean => type.Equals(ShaderType.Bool),
        Value.Address a => a.Storage.Type.Equals(type),
        _ => throw new NotSupportedException($"Unsupported value {value}")
    };

    internal static Value Binary(IBinaryOp op, Value left, Value right)
    {
        if ((left, right) is not ((Value.Integer, Value.Integer) or (Value.Boolean, Value.Boolean)))
            throw new NotSupportedException($"Unsupported binary operands {left}, {right}.");
        return op switch
        {
            BinaryRelational.Eq => new Value.Boolean(left == right),
            BinaryRelational.Ne => new Value.Boolean(left != right),
            BinaryRelational.Lt => new Value.Boolean(left.Int < right.Int),
            BinaryRelational.Le => new Value.Boolean(left.Int <= right.Int),
            BinaryRelational.Gt => new Value.Boolean(left.Int > right.Int),
            BinaryRelational.Ge => new Value.Boolean(left.Int >= right.Int),
            BinaryArithmetic.Add => new Value.Integer(unchecked(left.Int + right.Int)),
            BinaryArithmetic.Sub => new Value.Integer(unchecked(left.Int - right.Int)),
            BinaryArithmetic.Mul => new Value.Integer(unchecked(left.Int * right.Int)),
            _ => throw new NotSupportedException($"Unsupported scalar binary operation {op}")
        };
    }

    internal static Execution RunCfg(FunctionBody4 body, ImmutableArray<Value> arguments, int stepLimit = 10000)
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

    private static bool IsScalar(IShaderType type) => type.Equals(ShaderType.I32) || type.Equals(ShaderType.Bool);
}

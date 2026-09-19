using System.Collections.Immutable;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language;
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
        internal sealed record UnsignedInteger(uint Data) : Value;
        internal sealed record Float32(float Data) : Value;
        internal sealed record Float64(double Data) : Value;
        internal sealed record Boolean(bool Data) : Value;
        internal sealed record Address(IShaderValue Storage) : Value;

        internal int Int => this is Integer i ? i.Data : throw new NotSupportedException($"Expected i32, got {this}");
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

    internal static Execution RunCfg(FunctionBody4 body, ImmutableArray<Value> arguments, int stepLimit = 10000)
        => Run(
            body.Declaration,
            body.Entry,
            label => body[label].Parameters,
            label => body[label].Body.Elements,
            label => body[label].Body.Last,
            arguments,
            stepLimit,
            "CFG");

    internal static Execution RunValueCfg(
        CilValueControlFlowBody body,
        ImmutableArray<Value> arguments,
        int stepLimit = 10000) =>
        Run(
            body.Declaration,
            body.Graph.EntryLabel,
            label => body.Graph[label].Parameters,
            label => body.Graph[label].Body.Elements,
            label => body.Graph[label].Body.Last,
            arguments,
            stepLimit,
            "value CFG");

    private static Execution Run(
        FunctionDeclaration declaration,
        Label entry,
        Func<Label, ImmutableArray<IShaderValue>> parametersAt,
        Func<Label, IEnumerable<Instruction<IShaderValue, IShaderValue>>> instructionsAt,
        Func<Label, ITerminator<RegionJump<IShaderValue>, IShaderValue>> terminatorAt,
        ImmutableArray<Value> arguments,
        int stepLimit,
        string stage)
    {
        var context = $"{stage} {declaration.Name}";
        if (!IsScalar(declaration.ReturnType) || declaration.Parameters.Any(p => !IsScalar(p.Type)))
            throw new NotSupportedException($"{context}: unsupported scalar function signature.");
        var budget = new Budget(context, stepLimit);
        var values = new Dictionary<IShaderValue, Value>();
        var memory = new Dictionary<IShaderValue, Value>();
        var trace = ImmutableArray.CreateBuilder<Label>();
        if (arguments.Length != declaration.Parameters.Length)
            throw new InvalidOperationException($"{context}: incorrect argument count.");
        foreach (var (parameter, argument) in declaration.Parameters.Zip(arguments))
            Write(memory, parameter.Value, argument, parameter.Type);

        Value Read(IShaderValue? value) => value switch
        {
            LiteralValue { Value: I32Literal i } => new Value.Integer(i.Value),
            LiteralValue { Value: U32Literal i } => new Value.UnsignedInteger(i.Value),
            LiteralValue { Value: F32Literal f } => new Value.Float32(f.Value),
            LiteralValue { Value: F64Literal f } => new Value.Float64(f.Value),
            LiteralValue { Value: BoolLiteral b } => new Value.Boolean(b.Value),
            ParameterPointerValue or VariablePointerValue => new Value.Address(value),
            not null when values.TryGetValue(value, out var result) => result,
            _ => throw new NotSupportedException($"{context}: unsupported or undefined value {value}.")
        };

        void Write(Dictionary<IShaderValue, Value> target, IShaderValue key, Value value, IShaderType type)
        {
            if (!HasType(value, type))
                throw new NotSupportedException($"{context}: expected {type.Name}, got {value}.");
            target[key] = value;
        }

        var label = entry;
        while (true)
        {
            budget.Step(label.ToString());
            trace.Add(label);
            foreach (var instruction in instructionsAt(label))
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
                    default:
                        throw new NotSupportedException(
                            $"{context}, {label}: unsupported operation {instruction.Operation.Name}.");
                }
                var destination = instruction.Result ??
                    throw new InvalidOperationException($"{context}, {label}: missing instruction result.");
                Write(values, destination, result, destination.Type);
            }

            budget.Step($"{label}: terminator");
            var terminator = terminatorAt(label);
            if (terminator is Terminator.D.ReturnExpr<RegionJump<IShaderValue>, IShaderValue> returned)
            {
                var result = Read(returned.Expr);
                if (!HasType(result, declaration.ReturnType))
                    throw new NotSupportedException(
                        $"{context}, {label}: return value does not match {declaration.ReturnType.Name}.");
                return new Execution(result, trace.ToImmutable());
            }
            var jump = terminator switch
            {
                Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue> branch => branch.Target,
                Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue> branch =>
                    Read(branch.Condition).Bool ? branch.TrueTarget : branch.FalseTarget,
                _ => throw new NotSupportedException($"{context}, {label}: unsupported terminator {terminator}.")
            };
            var parameters = parametersAt(jump.Label);
            if (parameters.Length != jump.Arguments.Length)
                throw new InvalidOperationException($"{context}: invalid edge {label} -> {jump.Label} arity.");
            // All sources are read before any destination is overwritten (parallel edge copies).
            var incoming = jump.Arguments.Select(a => Read(a)).ToImmutableArray();
            foreach (var (parameter, argument) in parameters.Zip(incoming))
                Write(values, parameter, argument, parameter.Type);
            label = jump.Label;
        }
    }

    internal static void AssertEquivalent(Execution expected, Execution actual)
    {
        Assert.True(expected.Trace.SequenceEqual(actual.Trace),
            $"Expected result: {expected.Result}; actual: {actual.Result}\n" +
            $"Original blocks: {string.Join(" -> ", expected.Trace)}\nExecuted blocks: {string.Join(" -> ", actual.Trace)}");
        Assert.Equal(expected.Result, actual.Result);
    }

    private static bool IsScalar(IShaderType type) =>
        type.Equals(ShaderType.I32) ||
        type.Equals(ShaderType.U32) ||
        type.Equals(ShaderType.F32) ||
        type.Equals(ShaderType.F64) ||
        type.Equals(ShaderType.Bool);
}

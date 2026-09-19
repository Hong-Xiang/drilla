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
using DualDrill.Common.Nat;
using static DualDrill.CLSL.Test.ScalarControlFlowOracle;

namespace DualDrill.CLSL.Test;

public sealed class PromoteLocalsPassTests
{
    private static readonly ITerminatorSemantic<RegionJump<IShaderValue>, IShaderValue,
            ITerminator<RegionJump<IShaderValue>, IShaderValue>>
        Terms = Terminator.Factory<RegionJump<IShaderValue>, IShaderValue>();

    [Theory]
    [InlineData(false, false, 30)]
    [InlineData(false, true, 20)]
    [InlineData(true, false, 10)]
    [InlineData(true, true, 10)]
    public void BranchAndNestedJoinsMatchScalarExecution(bool outer, bool inner, int expected)
    {
        var local = Local("value");
        var entry = Label.Create("entry");
        var left = Label.Create("left");
        var right = Label.Create("right");
        var nestedLeft = Label.Create("nested-left");
        var nestedRight = Label.Create("nested-right");
        var join = Label.Create("join");
        var loaded = Value(ShaderType.I32, "loaded");
        var graph = Graph(
            Block(entry, [], [],
                Terms.BrIf(Bool(outer), Jump(left), Jump(right))),
            Block(left, [], [Store(local, Int(10))], Terms.Br(Jump(join))),
            Block(right, [], [],
                Terms.BrIf(Bool(inner), Jump(nestedLeft), Jump(nestedRight))),
            Block(nestedLeft, [], [Store(local, Int(20))], Terms.Br(Jump(join))),
            Block(nestedRight, [], [Store(local, Int(30))], Terms.Br(Jump(join))),
            Block(join, [], [Load(local, loaded)], Terms.ReturnExpr(loaded)));

        var promoted = PromoteLocalsPass.Run(graph, [local], false);

        AssertEquivalent(graph, promoted, [local], false, expected);
        var parameter = Assert.Single(promoted[join].Parameters);
        Assert.True(parameter.Type.Equals(ShaderType.I32));
        Assert.DoesNotContain(promoted.Labels().SelectMany(label => promoted[label].Body.Elements),
            instruction => instruction.Operation is LoadOperation or StoreOperation);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(4, 6)]
    [InlineData(7, 21)]
    public void CounterAccumulatorLoopMatchesZeroAndMultipleIterations(int limit, int expected)
    {
        var counter = Local("counter");
        var sum = Local("sum");
        var entry = Label.Create("entry");
        var header = Label.Create("header");
        var body = Label.Create("body");
        var exit = Label.Create("exit");
        var currentCounter = Value(ShaderType.I32, "counter");
        var currentSum = Value(ShaderType.I32, "sum");
        var condition = Value(ShaderType.Bool, "condition");
        var nextSum = Value(ShaderType.I32, "next-sum");
        var nextCounter = Value(ShaderType.I32, "next-counter");
        var result = Value(ShaderType.I32, "result");
        var graph = Graph(
            Block(entry, [], [Store(counter, Int(0)), Store(sum, Int(0))], Terms.Br(Jump(header))),
            Block(header, [], [
                Load(counter, currentCounter),
                LessThan(condition, currentCounter, Int(limit))
            ], Terms.BrIf(condition, Jump(body), Jump(exit))),
            Block(body, [], [
                Load(sum, currentSum),
                Add(nextSum, currentSum, currentCounter),
                Store(sum, nextSum),
                Add(nextCounter, currentCounter, Int(1)),
                Store(counter, nextCounter)
            ], Terms.Br(Jump(header))),
            Block(exit, [], [Load(sum, result)], Terms.ReturnExpr(result)));

        var promoted = PromoteLocalsPass.Run(graph, [counter, sum], false);

        AssertEquivalent(graph, promoted, [counter, sum], false, expected);
        Assert.Equal(2, promoted[header].Parameters.Length);
        Assert.All(promoted[header].Parameters, parameter => Assert.True(parameter.Type.Equals(ShaderType.I32)));
    }

    [Theory]
    [InlineData(true, true, 10)]
    [InlineData(true, false, 11)]
    [InlineData(false, true, 20)]
    [InlineData(false, false, 21)]
    public void ExistingParameterPrefixesAndSameTargetArmsArePreserved(
        bool chooseSide,
        bool chooseArgument,
        int expected)
    {
        var local = Local("value");
        var entry = Label.Create("entry");
        var left = Label.Create("left");
        var right = Label.Create("right");
        var join = Label.Create("join");
        var existing = Value(ShaderType.I32, "stack");
        var loaded = Value(ShaderType.I32, "loaded");
        var graph = Graph(
            Block(entry, [], [],
                Terms.BrIf(Bool(chooseSide), Jump(left), Jump(right))),
            Block(left, [], [Store(local, Int(100))],
                Terms.BrIf(Bool(chooseArgument), Jump(join, Int(10)), Jump(join, Int(11)))),
            Block(right, [], [Store(local, Int(200))],
                Terms.BrIf(Bool(chooseArgument), Jump(join, Int(20)), Jump(join, Int(21)))),
            Block(join, [existing], [Load(local, loaded)], Terms.ReturnExpr(existing)));

        var promoted = PromoteLocalsPass.Run(graph, [local], false);

        AssertEquivalent(graph, promoted, [local], false, expected);
        Assert.Equal(2, promoted[join].Parameters.Length);
        Assert.Same(existing, promoted[join].Parameters[0]);
        Assert.True(promoted[join].Parameters[1].Type.Equals(ShaderType.I32));
        var leftTerminator = Assert.IsType<Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue>>(
            promoted[left].Body.Last);
        Assert.Equal(new I32Literal(10),
            Assert.IsType<I32Literal>(Assert.IsType<LiteralValue>(leftTerminator.TrueTarget.Arguments[0]).Value));
        Assert.Equal(new I32Literal(11),
            Assert.IsType<I32Literal>(Assert.IsType<LiteralValue>(leftTerminator.FalseTarget.Arguments[0]).Value));
        Assert.Equal(2, leftTerminator.TrueTarget.Arguments.Length);
        Assert.Equal(2, leftTerminator.FalseTarget.Arguments.Length);
    }

    [Fact]
    public void EscapedNeighborRemainsStorageWhileIndependentLocalPromotes()
    {
        var promotedLocal = Local("promoted");
        var escapedLocal = Local("escaped");
        var entry = Label.Create("entry");
        var exit = Label.Create("exit");
        var observedAddress = Value(escapedLocal.Value.Type, "observed-address");
        var loaded = Value(ShaderType.I32, "loaded");
        var graph = Graph(
            Block(entry, [], [
                Store(promotedLocal, Int(7)),
                Store(escapedLocal, Int(9))
            ], Terms.Br(Jump(exit, escapedLocal.Value))),
            Block(exit, [observedAddress], [
                Load(promotedLocal, loaded)
            ], Terms.ReturnExpr(loaded)));

        var promoted = PromoteLocalsPass.Run(graph, [promotedLocal, escapedLocal], false);

        AssertEquivalent(graph, promoted, [promotedLocal, escapedLocal], false, 7);
        Assert.DoesNotContain(promoted.Labels().SelectMany(label => promoted[label].Body.Elements),
            instruction => ReferenceEquals(instruction.Operand0, promotedLocal.Value));
        Assert.Contains(promoted[entry].Body.Elements,
            instruction => instruction.Operation is StoreOperation &&
                           ReferenceEquals(instruction.Operand0, escapedLocal.Value));
        var jump = Assert.IsType<Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue>>(
            promoted[entry].Body.Last).Target;
        Assert.Same(escapedLocal.Value, Assert.Single(jump.Arguments));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InitLocalsSeedsExactTypedZeroValues(bool boolean)
    {
        IShaderType type = boolean ? ShaderType.Bool : ShaderType.I32;
        var local = Local("seed", type);
        var entry = Label.Create("entry");
        var loaded = Value(type, "loaded");
        var graph = Graph(Block(entry, [], [Load(local, loaded)], Terms.ReturnExpr(loaded)));

        var promoted = PromoteLocalsPass.Run(graph, [local], true);

        var expected = boolean ? (Value)new Value.Boolean(false) : new Value.Integer(0);
        Assert.Equal(expected, RunCfg(graph, [local], true).Result);
        Assert.Equal(expected, RunCfg(promoted, [local], true).Result);
        var returned = Assert.IsType<Terminator.D.ReturnExpr<RegionJump<IShaderValue>, IShaderValue>>(
            promoted[entry].Body.Last).Expr;
        var literal = Assert.IsType<LiteralValue>(returned);
        if (boolean)
            Assert.Equal(new BoolLiteral(false), Assert.IsType<BoolLiteral>(literal.Value));
        else
            Assert.Equal(new I32Literal(0), Assert.IsType<I32Literal>(literal.Value));
    }

    [Fact]
    public void MissingDefinitionOnOneBranchRemainsStorage()
    {
        var local = Local("value");
        var entry = Label.Create("entry");
        var stored = Label.Create("stored");
        var missing = Label.Create("missing");
        var join = Label.Create("join");
        var loaded = Value(ShaderType.I32, "loaded");
        var graph = Graph(
            Block(entry, [], [], Terms.BrIf(Bool(true), Jump(stored), Jump(missing))),
            Block(stored, [], [Store(local, Int(1))], Terms.Br(Jump(join))),
            Block(missing, [], [], Terms.Br(Jump(join))),
            Block(join, [], [Load(local, loaded)], Terms.ReturnExpr(loaded)));

        Assert.Same(graph, PromoteLocalsPass.Run(graph, [local], false));
    }

    [Fact]
    public void BackedgeStoreCannotBootstrapMissingFirstIterationDefinition()
    {
        var local = Local("value");
        var entry = Label.Create("entry");
        var header = Label.Create("header");
        var body = Label.Create("body");
        var exit = Label.Create("exit");
        var loaded = Value(ShaderType.I32, "loaded");
        var graph = Graph(
            Block(entry, [], [], Terms.Br(Jump(header, Bool(true)))),
            Block(header, [Value(ShaderType.Bool, "again")], [Load(local, loaded)],
                Terms.BrIf(Bool(true), Jump(body), Jump(exit))),
            Block(body, [], [Store(local, Int(1))], Terms.Br(Jump(header, Bool(false)))),
            Block(exit, [], [], Terms.ReturnExpr(loaded)));

        Assert.Same(graph, PromoteLocalsPass.Run(graph, [local], false));
    }

    [Fact]
    public void PreheaderInitializedLoopInvariantPromotesWithoutBackedgeStore()
    {
        var local = Local("invariant");
        var entry = Label.Create("entry");
        var header = Label.Create("header");
        var body = Label.Create("body");
        var exit = Label.Create("exit");
        var again = Value(ShaderType.Bool, "again");
        var loaded = Value(ShaderType.I32, "loaded");
        var graph = Graph(
            Block(entry, [], [Store(local, Int(7))], Terms.Br(Jump(header, Bool(true)))),
            Block(header, [again], [], Terms.BrIf(again, Jump(body), Jump(exit))),
            Block(body, [], [], Terms.Br(Jump(header, Bool(false)))),
            Block(exit, [], [Load(local, loaded)], Terms.ReturnExpr(loaded)));

        var promoted = PromoteLocalsPass.Run(graph, [local], false);

        AssertEquivalent(graph, promoted, [local], false, 7);
        Assert.Single(promoted[header].Parameters);
        Assert.DoesNotContain(promoted.Labels().SelectMany(label => promoted[label].Body.Elements),
            instruction => instruction.Operation is LoadOperation or StoreOperation);
    }

    [Fact]
    public void EntryBackedgeLocalStaysStorageButKillBeforeUseNeighborPromotes()
    {
        var carried = Local("carried");
        var counter = Local("counter");
        var killed = Local("killed");
        var entry = Label.Create("entry");
        var exit = Label.Create("exit");
        var current = Value(ShaderType.I32, "current");
        var next = Value(ShaderType.I32, "next");
        var index = Value(ShaderType.I32, "index");
        var nextIndex = Value(ShaderType.I32, "next-index");
        var again = Value(ShaderType.Bool, "again");
        var killedValue = Value(ShaderType.I32, "killed-value");
        var result = Value(ShaderType.I32, "result");
        var graph = Graph(
            Block(entry, [], [
                Load(carried, current),
                Add(next, current, Int(1)),
                Store(carried, next),
                Load(counter, index),
                Add(nextIndex, index, Int(1)),
                Store(counter, nextIndex),
                LessThan(again, nextIndex, Int(2)),
                Store(killed, Int(9)),
                Load(killed, killedValue)
            ], Terms.BrIf(again, Jump(entry), Jump(exit))),
            Block(exit, [], [Load(carried, result)], Terms.ReturnExpr(result)));

        var promoted = PromoteLocalsPass.Run(graph, [carried, counter, killed], true);

        AssertEquivalent(graph, promoted, [carried, counter, killed], true, 2);
        Assert.Contains(promoted[entry].Body.Elements,
            instruction => instruction.Operation is LoadOperation &&
                           ReferenceEquals(instruction.Operand0, carried.Value));
        Assert.Contains(promoted[entry].Body.Elements,
            instruction => instruction.Operation is StoreOperation &&
                           ReferenceEquals(instruction.Operand0, counter.Value));
        Assert.DoesNotContain(promoted[entry].Body.Elements,
            instruction => ReferenceEquals(instruction.Operand0, killed.Value));
    }

    [Fact]
    public void CyclicTwoLocalSwapUsesParallelBackedgeSnapshots()
    {
        var a = Local("a");
        var b = Local("b");
        var entry = Label.Create("entry");
        var header = Label.Create("header");
        var swap = Label.Create("swap");
        var exit = Label.Create("exit");
        var again = Value(ShaderType.Bool, "again");
        var oldA = Value(ShaderType.I32, "old-a");
        var oldB = Value(ShaderType.I32, "old-b");
        var finalA = Value(ShaderType.I32, "final-a");
        var finalB = Value(ShaderType.I32, "final-b");
        var tens = Value(ShaderType.I32, "tens");
        var result = Value(ShaderType.I32, "result");
        var graph = Graph(
            Block(entry, [], [Store(a, Int(1)), Store(b, Int(2))],
                Terms.Br(Jump(header, Bool(true)))),
            Block(header, [again], [],
                Terms.BrIf(again, Jump(swap), Jump(exit))),
            Block(swap, [], [
                Load(a, oldA),
                Load(b, oldB),
                Store(a, oldB),
                Store(b, oldA)
            ], Terms.Br(Jump(header, Bool(false)))),
            Block(exit, [], [
                Load(a, finalA),
                Load(b, finalB),
                Multiply(tens, finalA, Int(10)),
                Add(result, tens, finalB)
            ], Terms.ReturnExpr(result)));

        var promoted = PromoteLocalsPass.Run(graph, [a, b], false);

        AssertEquivalent(graph, promoted, [a, b], false, 21);
        Assert.Equal(3, promoted[header].Parameters.Length);
        var backedge = Assert.IsType<Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue>>(
            promoted[swap].Body.Last).Target;
        Assert.Equal(3, backedge.Arguments.Length);
        Assert.Same(promoted[header].Parameters[2], backedge.Arguments[1]);
        Assert.Same(promoted[header].Parameters[1], backedge.Arguments[2]);
    }

    [Fact]
    public void RetainedInstructionIdentityPayloadOrderTypesAndIdempotenceArePreserved()
    {
        var local = Local("value");
        var entry = Label.Create("entry");
        var loaded = Value(ShaderType.I32, "loaded");
        var result = Value(ShaderType.I32, "result");
        var operation = NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Add>.Instance;
        var payload = new object();
        var add = Instruction<IShaderValue, IShaderValue>.Create(
            operation,
            result,
            [loaded, Int(1)],
            payload);
        var graph = Graph(Block(entry, [], [
            Store(local, Int(5)),
            Load(local, loaded),
            add
        ], Terms.ReturnExpr(result)));

        var promoted = PromoteLocalsPass.Run(graph, [local], false);
        var retained = Assert.Single(promoted[entry].Body.Elements);

        Assert.Same(operation, retained.Operation);
        Assert.Same(result, retained.Result);
        Assert.Same(payload, retained.Payload);
        Assert.Equal(2, retained.OperandCount);
        Assert.Equal(new I32Literal(5), Assert.IsType<I32Literal>(
            Assert.IsType<LiteralValue>(retained.Operand0).Value));
        Assert.Equal(new I32Literal(1), Assert.IsType<I32Literal>(
            Assert.IsType<LiteralValue>(retained.Operand1).Value));
        Assert.Same(promoted, PromoteLocalsPass.Run(promoted, [local], false));
        AssertEquivalent(graph, promoted, [local], false, 6);
    }

    [Fact]
    public void UnsupportedScalarStorageIsKept()
    {
        var local = Local("float", ShaderType.F32);
        var entry = Label.Create("entry");
        var loaded = Value(ShaderType.F32, "loaded");
        var graph = Graph(Block(entry, [], [
            Store(local, ShaderValue.Literal(new F32Literal(1))),
            Load(local, loaded)
        ], Terms.ReturnExpr(loaded)));

        Assert.Same(graph, PromoteLocalsPass.Run(graph, [local], false));
    }

    [Fact]
    public void MalformedMemoryInstructionsAreRejectedBeforeEligibility()
    {
        var local = Local("value");
        var entry = Label.Create("entry");
        var value = Value(ShaderType.I32, "value");
        var malformedLoad = Instruction<IShaderValue, IShaderValue>.Create(
            new LoadOperation(),
            value,
            []);
        var malformedStore = Instruction<IShaderValue, IShaderValue>.Create(
            new StoreOperation(),
            value,
            [local.Value, Int(1)]);

        Assert.Throws<ArgumentException>(() => PromoteLocalsPass.Run(
            Graph(Block(entry, [], [malformedLoad], Terms.ReturnExpr(value))),
            [local],
            false));
        Assert.Throws<ArgumentException>(() => PromoteLocalsPass.Run(
            Graph(Block(entry, [], [malformedStore], Terms.ReturnExpr(value))),
            [local],
            false));
    }

    [Fact]
    public void MalformedEdgesAndDuplicateLocalsAreRejected()
    {
        var local = Local("value");
        var entry = Label.Create("entry");
        var target = Label.Create("target");
        var parameter = Value(ShaderType.I32, "parameter");
        var wrongArity = Graph(
            Block(entry, [], [], Terms.Br(Jump(target))),
            Block(target, [parameter], [], Terms.ReturnExpr(parameter)));
        var wrongType = Graph(
            Block(entry, [], [], Terms.Br(Jump(target, Bool(false)))),
            Block(target, [parameter], [], Terms.ReturnExpr(parameter)));

        Assert.Throws<ArgumentException>(() => PromoteLocalsPass.Run(wrongArity, [local], false));
        Assert.Throws<ArgumentException>(() => PromoteLocalsPass.Run(wrongType, [local], false));
        Assert.Throws<ArgumentException>(() => PromoteLocalsPass.Run(
            Graph(Block(entry, [], [], Terms.ReturnExpr(Int(0)))),
            [local, local],
            false));
    }

    [Fact]
    public void DisconnectedAndInconsistentGraphsAreRejected()
    {
        var entry = Label.Create("entry");
        var other = Label.Create("other");
        var disconnectedEntry = Block(entry, [], [], Terms.ReturnExpr(Int(0)));
        var disconnectedOther = Block(other, [], [], Terms.ReturnExpr(Int(1)));
        var disconnected = new ControlFlowGraph<CilValueBasicBlock>(entry,
            new Dictionary<Label, ControlFlowGraph<CilValueBasicBlock>.NodeDefinition>
            {
                [entry] = new(disconnectedEntry.Successor, disconnectedEntry),
                [other] = new(disconnectedOther.Successor, disconnectedOther)
            });
        var wrongLabel = Block(other, [], [], Terms.ReturnExpr(Int(0)));
        var inconsistentLabel = new ControlFlowGraph<CilValueBasicBlock>(entry,
            new Dictionary<Label, ControlFlowGraph<CilValueBasicBlock>.NodeDefinition>
            {
                [entry] = new(wrongLabel.Successor, wrongLabel)
            });
        var mismatchedBlock = Block(entry, [], [], Terms.ReturnExpr(Int(0)));
        var inconsistentSuccessor = new ControlFlowGraph<CilValueBasicBlock>(entry,
            new Dictionary<Label, ControlFlowGraph<CilValueBasicBlock>.NodeDefinition>
            {
                [entry] = new(new UnconditionalSuccessor(entry), mismatchedBlock)
            });
        var entryParameter = Value(ShaderType.I32, "entry-parameter");
        var parameterizedEntry = Graph(
            Block(entry, [entryParameter], [], Terms.ReturnExpr(entryParameter)));

        Assert.Throws<ArgumentException>(() => PromoteLocalsPass.Run(disconnected, [], false));
        Assert.Throws<ArgumentException>(() => PromoteLocalsPass.Run(inconsistentLabel, [], false));
        Assert.Throws<ArgumentException>(() => PromoteLocalsPass.Run(inconsistentSuccessor, [], false));
        Assert.Throws<ArgumentException>(() => PromoteLocalsPass.Run(parameterizedEntry, [], false));
    }

    private static void AssertEquivalent(
        ControlFlowGraph<CilValueBasicBlock> original,
        ControlFlowGraph<CilValueBasicBlock> promoted,
        ImmutableArray<VariableDeclaration> locals,
        bool initLocals,
        int expected)
    {
        var before = RunCfg(original, locals, initLocals);
        var after = RunCfg(promoted, locals, initLocals);
        Assert.Equal(new Value.Integer(expected), before.Result);
        Assert.Equal(before.Result, after.Result);
        Assert.True(before.Trace.SequenceEqual(after.Trace),
            $"Original: {string.Join(" -> ", before.Trace)}\nPromoted: {string.Join(" -> ", after.Trace)}");
    }

    private static ControlFlowGraph<CilValueBasicBlock> Graph(params CilValueBasicBlock[] blocks) =>
        new(
            blocks[0].Label,
            blocks.ToDictionary(
                block => block.Label,
                block => new ControlFlowGraph<CilValueBasicBlock>.NodeDefinition(block.Successor, block)));

    private static CilValueBasicBlock Block(
        Label label,
        ImmutableArray<IShaderValue> parameters,
        IEnumerable<Instruction<IShaderValue, IShaderValue>> instructions,
        ITerminator<RegionJump<IShaderValue>, IShaderValue> terminator) =>
        new(label, parameters, Seq.Create(instructions, terminator));

    private static VariableDeclaration Local(string name, IShaderType? type = null) =>
        new(FunctionAddressSpace.Instance, name, type ?? ShaderType.I32, []);

    private static IShaderValue Value(IShaderType type, string name) =>
        ShaderValue.Intermediate(type, name);

    private static IShaderValue Int(int value) => IntLiteral(value);

    private static LiteralValue IntLiteral(int value) => new(new I32Literal(value));

    private static IShaderValue Bool(bool value) => ShaderValue.Literal(new BoolLiteral(value));

    private static RegionJump<IShaderValue> Jump(Label label, params IShaderValue[] arguments) =>
        new(label, [.. arguments]);

    private static Instruction<IShaderValue, IShaderValue> Load(
        VariableDeclaration local,
        IShaderValue result) =>
        Instruction.Factory.Load(default, new LoadOperation(), result, local.Value);

    private static Instruction<IShaderValue, IShaderValue> Store(
        VariableDeclaration local,
        IShaderValue value) =>
        Instruction.Factory.Store(default, new StoreOperation(), local.Value, value);

    private static Instruction<IShaderValue, IShaderValue> Add(
        IShaderValue result,
        IShaderValue left,
        IShaderValue right) =>
        Instruction.Factory.Operation2(
            default,
            NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Add>.Instance,
            result,
            left,
            right);

    private static Instruction<IShaderValue, IShaderValue> Multiply(
        IShaderValue result,
        IShaderValue left,
        IShaderValue right) =>
        Instruction.Factory.Operation2(
            default,
            NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Mul>.Instance,
            result,
            left,
            right);

    private static Instruction<IShaderValue, IShaderValue> LessThan(
        IShaderValue result,
        IShaderValue left,
        IShaderValue right) =>
        Instruction.Factory.Operation2(
            default,
            NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Lt>.Instance,
            result,
            left,
            right);
}

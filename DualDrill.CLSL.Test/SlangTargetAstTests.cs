using System.Collections.Immutable;
using System.Globalization;
using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Transform;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Test.ShaderModule;
using DualDrill.Common.CodeTextWriter;
using DualDrill.Common.Nat;
using Xunit.Abstractions;
using static DualDrill.CLSL.Test.ScalarControlFlowOracle;

namespace DualDrill.CLSL.Test;

[Collection(SlangProcessTestCollection.Name)]
public sealed class SlangTargetAstTests(ITestOutputHelper output)
{
    private static readonly ITerminatorSemantic<RegionJump<IShaderValue>, IShaderValue,
            ITerminator<RegionJump<IShaderValue>, IShaderValue>>
        Terms = Terminator.Factory<RegionJump<IShaderValue>, IShaderValue>();

    [Fact]
    public void SharedEffectfulTailHasOneAstPlacement()
    {
        var source = Lower(((Func<int, int>)ScalarControlFlowFixtures.SharedTail).Method);
        var target = source.Target;
        var multiplyBindings = Statements(target.Body)
            .OfType<SlangBind>()
            .Count(binding =>
                binding.Instruction.Operation is IBinaryExpressionOperation
                {
                    BinaryOp: BinaryArithmetic.Mul
                });

        Assert.Equal(1, multiplyBindings);
        Assert.Equal(source.Slang, new SlangEmitter(source.Module).Emit());
        var firstDump = target.PrettyPrint();
        Assert.Equal(firstDump, target.PrettyPrint());
        Capture("shared-tail", source.Region, target.PrettyPrint(), source.Slang);
    }

    [Fact]
    public async Task DominatingEntryValueRemainsLiveThroughOrdinaryContinuation()
    {
        var entry = Label.Create("entry");
        var exit = Label.Create("exit");
        var value = ShaderValue.Intermediate(ShaderType.I32);
        var result = ShaderValue.Intermediate(ShaderType.I32);
        var declaration = Function("DominatingChain", ShaderType.I32);
        var entryBody = ShaderRegionBody.Create(
            entry,
            [],
            [Instruction.Factory.Literal(default, new LiteralOperation(), value, Int(1))],
            Terms.Br(new(exit, [])),
            exit);
        var exitBody = ShaderRegionBody.Create(
            exit,
            [],
            [
                Instruction.Factory.Operation2(
                    default,
                    NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Add>.Instance,
                    result,
                    value,
                    Int(2))
            ],
            Terms.ReturnExpr(result),
            null);
        var body = new FunctionBody4(
            declaration,
            RegionTree.Block(entry, [RegionTree.Block(exit, [], exitBody, null)], entryBody, exit));

        await AssertEmittedEquivalent(body, [], new Value.Integer(3));
        var target = Lower(body);
        Assert.Single(
            Statements(target.Body).OfType<SlangScope>(),
            scope => scope.OriginalLabel == entry);
        Assert.Single(
            Statements(target.Body).OfType<SlangScope>(),
            scope => scope.OriginalLabel == exit);
    }

    [Fact]
    public async Task DominatingEntryValueRemainsLiveThroughDiamond()
    {
        var entry = Label.Create("entry");
        var left = Label.Create("left");
        var right = Label.Create("right");
        var join = Label.Create("join");
        var choose = new ParameterDeclaration("choose", ShaderType.Bool, []);
        var condition = ShaderValue.Intermediate(ShaderType.Bool);
        var value = ShaderValue.Intermediate(ShaderType.I32);
        var result = ShaderValue.Intermediate(ShaderType.I32);
        var declaration = new FunctionDeclaration(
            "DominatingDiamond", [choose], new FunctionReturn(ShaderType.I32, []), []);
        var entryBody = ShaderRegionBody.Create(
            entry,
            [],
            [
                Instruction.Factory.Load(default, new LoadOperation(), condition, choose.Value),
                Instruction.Factory.Literal(default, new LiteralOperation(), value, Int(4))
            ],
            Terms.BrIf(condition, new(left, []), new(right, [])),
            join);
        var leftBody = ShaderRegionBody.Create(left, [], [], Terms.Br(new(join, [])), join);
        var rightBody = ShaderRegionBody.Create(right, [], [], Terms.Br(new(join, [])), join);
        var joinBody = ShaderRegionBody.Create(
            join,
            [],
            [
                Instruction.Factory.Operation2(
                    default,
                    NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Add>.Instance,
                    result,
                    value,
                    Int(2))
            ],
            Terms.ReturnExpr(result),
            null);
        var body = new FunctionBody4(
            declaration,
            RegionTree.Block(entry,
            [
                RegionTree.Block(join, [], joinBody, null),
                RegionTree.Block(left, [], leftBody, join),
                RegionTree.Block(right, [], rightBody, join)
            ], entryBody, join));

        await AssertEmittedEquivalent(body, [new Value.Boolean(true)], new Value.Integer(6));
        await AssertEmittedEquivalent(body, [new Value.Boolean(false)], new Value.Integer(6));
    }

    [Fact]
    public async Task LoopHeaderValueUsedAfterExitIsCaptured()
    {
        var entry = Label.Create("entry");
        var loop = Label.Create("loop");
        var exit = Label.Create("exit");
        var value = ShaderValue.Intermediate(ShaderType.I32);
        var result = ShaderValue.Intermediate(ShaderType.I32);
        var declaration = Function("LoopValueExit", ShaderType.I32);
        var entryBody = ShaderRegionBody.Create(entry, [], [], Terms.Br(new(loop, [])), loop);
        var loopBody = ShaderRegionBody.Create(
            loop,
            [],
            [Instruction.Factory.Literal(default, new LiteralOperation(), value, Int(1))],
            Terms.BrIf(
                ShaderValue.Literal(new BoolLiteral(false)),
                new(loop, []),
                new(exit, [])),
            exit);
        var exitBody = ShaderRegionBody.Create(
            exit,
            [],
            [
                Instruction.Factory.Operation2(
                    default,
                    NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Add>.Instance,
                    result,
                    value,
                    Int(2))
            ],
            Terms.ReturnExpr(result),
            null);
        var body = new FunctionBody4(
            declaration,
            RegionTree.Block(entry,
            [
                RegionTree.Block(exit, [], exitBody, null),
                RegionTree.Loop(loop, [], loopBody, exit, exit)
            ], entryBody, loop));

        await AssertEmittedEquivalent(body, [], new Value.Integer(3));
        var target = Lower(body);
        var capture = Assert.Single(
            target.Body.Statements.OfType<SlangDeclare>(),
            statement => statement.Variable.Name.StartsWith("capture_", StringComparison.Ordinal));
        Assert.Contains(
            Statements(target.Body).OfType<SlangAssign>(),
            assignment => assignment.Target is SlangVariablePlace { Variable: var variable }
                && ReferenceEquals(variable, capture.Variable));
    }

    [Fact]
    public void LoweringMakesDeclarationValueEffectAndAssignmentOrderExplicit()
    {
        var entry = Label.Create("entry");
        var declaration = Function("Ordered", ShaderType.Unit);
        var variable = new VariableDeclaration(FunctionAddressSpace.Instance, "r", ShaderType.I32, []);
        var loaded = ShaderValue.Intermediate(ShaderType.I32);
        var body = new FunctionBody4(
            declaration,
            RegionTree.Block(entry, [], ShaderRegionBody.Create(
                entry,
                [],
                [
                    Instruction.Factory.Store(default, new StoreOperation(), variable.Value, Int(1)),
                    Instruction.Factory.Load(default, new LoadOperation(), loaded, variable.Value),
                    Instruction.Factory.Nop(default, NopOperation.Instance)
                ],
                Terms.ReturnVoid(),
                null), null));

        var target = Lower(body);

        Assert.Same(variable, Assert.IsType<SlangDeclare>(target.Body.Statements[0]).Variable);
        var scope = Assert.Single(Statements(target.Body).OfType<SlangScope>());
        Assert.Collection(
            scope.Body.Statements,
            child => Assert.IsType<SlangAssign>(child),
            child => Assert.IsType<SlangBind>(child),
            child => Assert.IsType<SlangEffect>(child),
            child => Assert.IsType<SlangReturnVoid>(child));
    }

    [Fact]
    public void NestedLoopsUseUnmarkedCarriersAndExplicitTransfers()
    {
        var target = Lower(((Func<int, int, int>)ScalarControlFlowFixtures.NestedLoopControl).Method).Target;
        var loops = Statements(target.Body).OfType<SlangLoop>().ToArray();

        Assert.True(loops.Length > 2);
        Assert.Equal(2, loops.Count(loop => loop.OriginalLabel is not null));
        Assert.Contains(loops, loop => loop.OriginalLabel is null);
        Assert.Contains(Statements(target.Body), statement => statement is SlangBreak);
        Assert.Contains(Statements(target.Body), statement => statement is SlangContinue);
    }

    [Fact]
    public void SharedTerminalDefinitionHasOneAstPlacement()
    {
        var entry = Label.Create("entry");
        var terminal = Label.Create("terminal");
        var condition = ShaderValue.Literal(new BoolLiteral(true));
        var declaration = Function("TerminalClone", ShaderType.I32);
        var entryBody = ShaderRegionBody.Create(
            entry,
            [],
            [],
            Terms.BrIf(condition, new(terminal, []), new(terminal, [])),
            null);
        var terminalBody = ShaderRegionBody.Create(
            terminal,
            [],
            [],
            Terms.ReturnExpr(Int(7)),
            null);
        var body = new FunctionBody4(
            declaration,
            RegionTree.Block(entry, [RegionTree.Block(terminal, [], terminalBody, null)], entryBody, null));

        var target = Lower(body);
        var conditional = Assert.Single(Statements(target.Body).OfType<SlangIf>());

        Assert.DoesNotContain(Statements(conditional.WhenTrue), statement => statement is SlangReturnValue);
        Assert.DoesNotContain(Statements(conditional.WhenFalse), statement => statement is SlangReturnValue);
        Assert.Single(Statements(target.Body).OfType<SlangReturnValue>());
        Assert.Single(
            Statements(target.Body).OfType<SlangScope>(),
            scope => scope.OriginalLabel == terminal);
    }

    [Fact]
    public void SharedEffectfulNonterminalDefinitionHasOneAstPlacement()
    {
        var entry = Label.Create("entry");
        var shared = Label.Create("shared");
        var terminal = Label.Create("terminal");
        var condition = ShaderValue.Literal(new BoolLiteral(true));
        var declaration = Function("RepeatedNonterminal", ShaderType.Unit);
        var entryBody = ShaderRegionBody.Create(
            entry,
            [],
            [],
            Terms.BrIf(condition, new(shared, []), new(shared, [])),
            null);
        var sharedBody = ShaderRegionBody.Create(
            shared,
            [],
            [Instruction.Factory.Nop(default, NopOperation.Instance)],
            Terms.Br(new(terminal, [])),
            null);
        var terminalBody = ShaderRegionBody.Create(terminal, [], [], Terms.ReturnVoid(), null);
        var body = new FunctionBody4(
            declaration,
            RegionTree.Block(entry,
            [
                RegionTree.Block(terminal, [], terminalBody, null),
                RegionTree.Block(shared, [], sharedBody, null)
            ], entryBody, null));

        var target = Lower(body);

        Assert.Single(
            Statements(target.Body).OfType<SlangScope>(),
            scope => scope.OriginalLabel == shared);
        Assert.Single(Statements(target.Body).OfType<SlangEffect>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ClonedDirectEdgeContinuationExecutesOnce(bool chooseLeft)
    {
        var entry = Label.Create("entry");
        var left = Label.Create("left");
        var right = Label.Create("right");
        var effect = Label.Create("effect");
        var join = Label.Create("join");
        var choose = new ParameterDeclaration("choose", ShaderType.Bool, []);
        var condition = ShaderValue.Intermediate(ShaderType.Bool);
        var counter = new VariableDeclaration(FunctionAddressSpace.Instance, "counter", ShaderType.I32, []);
        var before = ShaderValue.Intermediate(ShaderType.I32);
        var after = ShaderValue.Intermediate(ShaderType.I32);
        var result = ShaderValue.Intermediate(ShaderType.I32);
        var declaration = new FunctionDeclaration(
            "DirectEdgeClone", [choose], new FunctionReturn(ShaderType.I32, []), []);
        var entryBody = ShaderRegionBody.Create(
            entry,
            [],
            [
                Instruction.Factory.Store(default, new StoreOperation(), counter.Value, Int(0)),
                Instruction.Factory.Load(default, new LoadOperation(), condition, choose.Value)
            ],
            Terms.BrIf(condition, new(left, []), new(right, [])),
            join);
        var leftBody = ShaderRegionBody.Create(left, [], [], Terms.Br(new(effect, [])), join);
        var rightBody = ShaderRegionBody.Create(right, [], [], Terms.Br(new(effect, [])), join);
        var effectBody = ShaderRegionBody.Create(
            effect,
            [],
            [
                Instruction.Factory.Load(default, new LoadOperation(), before, counter.Value),
                Instruction.Factory.Operation2(
                    default,
                    NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Add>.Instance,
                    after,
                    before,
                    Int(1)),
                Instruction.Factory.Store(default, new StoreOperation(), counter.Value, after)
            ],
            Terms.Br(new(join, [])),
            join);
        var joinBody = ShaderRegionBody.Create(
            join,
            [],
            [Instruction.Factory.Load(default, new LoadOperation(), result, counter.Value)],
            Terms.ReturnExpr(result),
            null);
        var body = new FunctionBody4(
            declaration,
            RegionTree.Block(entry,
            [
                RegionTree.Block(join, [], joinBody, null),
                RegionTree.Block(effect, [], effectBody, join),
                RegionTree.Block(left, [], leftBody, join),
                RegionTree.Block(right, [], rightBody, join)
            ], entryBody, join));

        var execution = await AssertEmittedEquivalent(
            body, [new Value.Boolean(chooseLeft)], new Value.Integer(1));

        Assert.Equal(1, execution.Trace.Count(label => label == effect));
        Assert.Equal(1, Statements(Lower(body).Body).OfType<SlangScope>()
            .Count(scope => scope.OriginalLabel == effect));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ClonedLoopTransferContinuationExecutesOnce(bool chooseLeft)
    {
        var entry = Label.Create("entry");
        var loop = Label.Create("loop");
        var chooseBlock = Label.Create("choose");
        var left = Label.Create("left");
        var right = Label.Create("right");
        var latch = Label.Create("latch");
        var exit = Label.Create("exit");
        var choose = new ParameterDeclaration("choose", ShaderType.Bool, []);
        var selected = ShaderValue.Intermediate(ShaderType.Bool);
        var counter = new VariableDeclaration(FunctionAddressSpace.Instance, "counter", ShaderType.I32, []);
        var current = ShaderValue.Intermediate(ShaderType.I32);
        var more = ShaderValue.Intermediate(ShaderType.Bool);
        var before = ShaderValue.Intermediate(ShaderType.I32);
        var after = ShaderValue.Intermediate(ShaderType.I32);
        var result = ShaderValue.Intermediate(ShaderType.I32);
        var declaration = new FunctionDeclaration(
            "LoopTransferClone", [choose], new FunctionReturn(ShaderType.I32, []), []);
        var entryBody = ShaderRegionBody.Create(
            entry,
            [],
            [
                Instruction.Factory.Store(default, new StoreOperation(), counter.Value, Int(0)),
                Instruction.Factory.Load(default, new LoadOperation(), selected, choose.Value)
            ],
            Terms.Br(new(loop, [])),
            loop);
        var loopBody = ShaderRegionBody.Create(
            loop,
            [],
            [
                Instruction.Factory.Load(default, new LoadOperation(), current, counter.Value),
                Instruction.Factory.Operation2(
                    default,
                    NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Lt>.Instance,
                    more,
                    current,
                    Int(1))
            ],
            Terms.BrIf(more, new(chooseBlock, []), new(exit, [])),
            exit);
        var chooseBody = ShaderRegionBody.Create(
            chooseBlock,
            [],
            [],
            Terms.BrIf(selected, new(left, []), new(right, [])),
            loop);
        var leftBody = ShaderRegionBody.Create(left, [], [], Terms.Br(new(latch, [])), loop);
        var rightBody = ShaderRegionBody.Create(right, [], [], Terms.Br(new(latch, [])), loop);
        var latchBody = ShaderRegionBody.Create(
            latch,
            [],
            [
                Instruction.Factory.Load(default, new LoadOperation(), before, counter.Value),
                Instruction.Factory.Operation2(
                    default,
                    NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Add>.Instance,
                    after,
                    before,
                    Int(1)),
                Instruction.Factory.Store(default, new StoreOperation(), counter.Value, after)
            ],
            Terms.Br(new(loop, [])),
            loop);
        var exitBody = ShaderRegionBody.Create(
            exit,
            [],
            [Instruction.Factory.Load(default, new LoadOperation(), result, counter.Value)],
            Terms.ReturnExpr(result),
            null);
        var body = new FunctionBody4(
            declaration,
            RegionTree.Block(entry,
            [
                RegionTree.Block(exit, [], exitBody, null),
                RegionTree.Loop(loop,
                [
                    RegionTree.Block(latch, [], latchBody, loop),
                    RegionTree.Block(left, [], leftBody, loop),
                    RegionTree.Block(right, [], rightBody, loop),
                    RegionTree.Block(chooseBlock, [], chooseBody, loop)
                ], loopBody, exit, exit)
            ], entryBody, loop));

        var execution = await AssertEmittedEquivalent(
            body, [new Value.Boolean(chooseLeft)], new Value.Integer(1));

        Assert.Equal(1, execution.Trace.Count(label => label == latch));
        Assert.Equal(1, Statements(Lower(body).Body).OfType<SlangScope>()
            .Count(scope => scope.OriginalLabel == latch));
    }

    [Fact]
    public async Task RegionParametersLowerToSelectedEdgeSlots()
    {
        var entry = Label.Create("entry");
        var target = Label.Create("target");
        var parameter = ShaderValue.Intermediate(ShaderType.I32);
        var declaration = Function("ResidualParameter", ShaderType.I32);
        var entryBody = ShaderRegionBody.Create(
            entry, [], [], Terms.Br(new(target, [Int(7)])), target);
        var targetBody = ShaderRegionBody.Create(
            target, [parameter], [], Terms.ReturnExpr(parameter), null);
        var body = new FunctionBody4(
            declaration,
            RegionTree.Block(
                entry,
                [RegionTree.Block(target, [], targetBody, null)],
                entryBody,
                target));

        var lowered = Lower(body);

        Assert.Contains(
            lowered.Body.Statements.OfType<SlangDeclare>(),
            declaration => declaration.Variable.Name.StartsWith("parameter_", StringComparison.Ordinal));
        await AssertEmittedEquivalent(body, [], new Value.Integer(7));
    }

    [Fact]
    public async Task SameTargetConditionalArgumentsRemainSelected()
    {
        var entry = Label.Create("entry");
        var exit = Label.Create("exit");
        var choose = new ParameterDeclaration("choose", ShaderType.Bool, []);
        var condition = ShaderValue.Intermediate(ShaderType.Bool);
        var parameter = ShaderValue.Intermediate(ShaderType.I32);
        var declaration = new FunctionDeclaration(
            "SelectedArgument", [choose], new FunctionReturn(ShaderType.I32, []), []);
        var entryBody = ShaderRegionBody.Create(
            entry,
            [],
            [Instruction.Factory.Load(default, new LoadOperation(), condition, choose.Value)],
            Terms.BrIf(condition, new(exit, [Int(10)]), new(exit, [Int(20)])),
            exit);
        var exitBody = ShaderRegionBody.Create(exit, [parameter], [], Terms.ReturnExpr(parameter), null);
        var body = new FunctionBody4(
            declaration,
            RegionTree.Block(entry, [RegionTree.Block(exit, [], exitBody, null)], entryBody, exit));

        var selectedTrue = await AssertEmittedEquivalent(
            body, [new Value.Boolean(true)], new Value.Integer(10));
        var selectedFalse = await AssertEmittedEquivalent(
            body, [new Value.Boolean(false)], new Value.Integer(20));
        var target = Lower(body);
        var source = Emit(target);
        Assert.Single(
            Statements(target.Body).OfType<SlangScope>(),
            scope => scope.OriginalLabel == exit);
        Capture("same-target-selected", body, target.PrettyPrint(), source);
        CaptureText(
            "same-target-selected.execution.txt",
            $"arguments=true result={selectedTrue.Result} trace={string.Join(" -> ", selectedTrue.Trace)}" +
            Environment.NewLine +
            $"arguments=false result={selectedFalse.Result} trace={string.Join(" -> ", selectedFalse.Trace)}" +
            Environment.NewLine);
    }

    [Fact]
    public void MissingLabelsAreRejectedBeforeLayout()
    {
        var entry = Label.Create("entry");
        var unreachable = Label.Create("unreachable");
        var missing = Label.Create("missing");
        var declaration = Function("MissingLabel", ShaderType.Unit);
        var entryBody = ShaderRegionBody.Create(entry, [], [], Terms.ReturnVoid(), null);
        var unreachableBody = ShaderRegionBody.Create(
            unreachable, [], [], Terms.Br(new(missing, [])), null);
        var error = Assert.Throws<ArgumentException>(() => new FunctionBody4(
            declaration,
            RegionTree.Block(
                entry,
                [RegionTree.Block(unreachable, [], unreachableBody, null)],
                entryBody,
                null)));

        Assert.Contains("unknown label", error.Message);
    }

    [Fact]
    public void UnownedExpansionCyclesAreRejected()
    {
        var left = Label.Create("left");
        var right = Label.Create("right");
        var declaration = Function("Cycle", ShaderType.Unit);
        var leftBody = ShaderRegionBody.Create(left, [], [], Terms.Br(new(right, [])), null);
        var rightBody = ShaderRegionBody.Create(right, [], [], Terms.Br(new(left, [])), null);
        var error = Assert.Throws<ArgumentException>(() => new FunctionBody4(
            declaration,
            RegionTree.Block(
                left,
                [RegionTree.Block(right, [], rightBody, null)],
                leftBody,
                null)));

        Assert.Contains("not visible", error.Message);
    }

    [Fact]
    public void MissingFunctionBodiesAreRejected()
    {
        var declaration = Function("MissingBody", ShaderType.Unit);
        var module = new ShaderModuleDeclaration<FunctionBody4>(
            [declaration],
            ImmutableDictionary<FunctionDeclaration, FunctionBody4>.Empty);

        var error = Assert.Throws<NotSupportedException>(
            () => new SlangTargetLowering().Lower(module));

        Assert.Contains("requires bodies for functions: MissingBody", error.Message);
    }

    [Fact]
    public void AddressProjectionBecomesTypedPlaceSyntax()
    {
        var entry = Label.Create("entry");
        var vectorType = VecType<N2, FloatType<N32>>.Instance;
        var declaration = Function("Projection", ShaderType.Unit);
        var vector = new VariableDeclaration(FunctionAddressSpace.Instance, "value", vectorType, []);
        var component = ShaderValue.Intermediate(ShaderType.F32.GetPtrType());
        var body = new FunctionBody4(
            declaration,
            RegionTree.Block(entry, [], ShaderRegionBody.Create(
                entry,
                [],
                [
                    Instruction.Factory.AddressOfChain(
                        default,
                        new AddressOfVecComponentOperation(vectorType, Swizzle.X.Instance),
                        component,
                        vector.Value),
                    Instruction.Factory.Store(
                        default,
                        new StoreOperation(),
                        component,
                        ShaderValue.Literal(new F32Literal(2.5f)))
                ],
                Terms.ReturnVoid(),
                null), null));

        var target = Lower(body);
        var assignment = Assert.Single(Statements(target.Body).OfType<SlangAssign>());
        var place = Assert.IsType<SlangComponentPlace>(assignment.Target);
        Assert.Equal("x", place.Component);
        Assert.Equal(ShaderType.F32, place.Type);
        Assert.DoesNotContain(
            Statements(target.Body),
            statement => statement is SlangBind
            {
                Instruction.Operation: IAddressOfOperation
            });
        Assert.Contains(".x = 2.5;", Emit(target));
    }

    [Fact]
    public void MemberAddressProjectionBecomesTypedPlaceSyntax()
    {
        var entry = Label.Create("entry");
        var member = new MemberDeclaration("field", ShaderType.F32, []);
        var structure = new StructureDeclaration { Name = "Holder", Members = [member] };
        var holder = new VariableDeclaration(
            FunctionAddressSpace.Instance,
            "holder",
            new StructureType(structure),
            []);
        var address = ShaderValue.Intermediate(ShaderType.F32.GetPtrType());
        var declaration = Function("MemberProjection", ShaderType.Unit);
        var body = new FunctionBody4(
            declaration,
            RegionTree.Block(entry, [], ShaderRegionBody.Create(
                entry,
                [],
                [
                    Instruction.Factory.AddressOfChain(
                        default,
                        new AddressOfMemberOperation(member),
                        address,
                        holder.Value),
                    Instruction.Factory.Store(
                        default,
                        new StoreOperation(),
                        address,
                        ShaderValue.Literal(new F32Literal(1.5f)))
                ],
                Terms.ReturnVoid(),
                null), null));

        var target = Lower(body);
        var place = Assert.IsType<SlangMemberPlace>(
            Assert.Single(Statements(target.Body).OfType<SlangAssign>()).Target);

        Assert.Same(member, place.Member);
        Assert.Equal(ShaderType.F32, place.Type);
        Assert.Contains(".field = 1.5;", Emit(target));
    }

    [Fact]
    public void LoweringStateIsIsolatedPerFunction()
    {
        var sharedLabel = Label.Create("shared-label-object");
        var left = Function("Left", ShaderType.I32);
        var right = Function("Right", ShaderType.I32);
        FunctionBody4 Body(FunctionDeclaration declaration, int value) =>
            new(
                declaration,
                RegionTree.Block(
                    sharedLabel,
                    [],
                    ShaderRegionBody.Create(
                        sharedLabel, [], [], Terms.ReturnExpr(Int(value)), null),
                    null));
        var source = new ShaderModuleDeclaration<FunctionBody4>(
            [left, right],
            ImmutableDictionary<FunctionDeclaration, FunctionBody4>.Empty
                .Add(left, Body(left, 1))
                .Add(right, Body(right, 2)));

        var target = new SlangTargetLowering().Lower(source);

        Assert.Equal(2, target.FunctionDefinitions.Count);
        Assert.Single(target.GetBody(left).Labels);
        Assert.Single(target.GetBody(right).Labels);
    }

    [Fact]
    public void AstOnlyEmitterIsDeterministicAndHasNoRegionInput()
    {
        var declaration = Function("AstOnly", ShaderType.I32);
        var variable = new VariableDeclaration(FunctionAddressSpace.Instance, "answer", ShaderType.I32, []);
        var target = new SlangFunctionBody(
            declaration,
            new SlangBlock(
            [
                new SlangDeclare(variable),
                new SlangAssign(new SlangVariablePlace(variable), new SlangValueOperand(Int(42))),
                new SlangReturnValue(new SlangPlaceOperand(new SlangVariablePlace(variable)))
            ]));
        var module = new ShaderModuleDeclaration<SlangFunctionBody>(
            [declaration],
            ImmutableDictionary<FunctionDeclaration, SlangFunctionBody>.Empty.Add(declaration, target));
        var emitter = new SlangEmitter(module);

        var first = emitter.Emit();
        var second = emitter.Emit();

        Assert.Equal(first, second);
        Assert.Equal(
        [
            "slang-target AstOnly",
            "declare %0(answer) : i32",
            "assign %0(answer) <- 42_i32",
            "return read %0(answer)"
        ], target.PrettyPrint().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains("var v_0_answer : i32;", first);
        Assert.Contains("v_0_answer = 42;", first);
        Assert.Contains("return v_0_answer;", first);
    }

    [Fact]
    public void LiteralSyntaxUsesLowercaseBooleansAndInvariantNumbers()
    {
        var declaration = Function("Literals", ShaderType.Unit);
        var trueResult = ShaderValue.Intermediate(ShaderType.Bool);
        var falseResult = ShaderValue.Intermediate(ShaderType.Bool);
        var floatResult = ShaderValue.Intermediate(ShaderType.F32);
        SlangBind Bind(IShaderValue result, ILiteral literal) =>
            new(Instruction<SlangOperand, IShaderValue>.Create(
                new LiteralOperation(),
                result,
                [new SlangValueOperand(ShaderValue.Literal(literal))]));
        var trueBinding = Bind(trueResult, new BoolLiteral(true));
        var target = new SlangFunctionBody(
            declaration,
            new SlangBlock(
            [
                trueBinding,
                Bind(falseResult, new BoolLiteral(false)),
                Bind(floatResult, new F32Literal(1.5f)),
                new SlangReturnVoid()
            ]));
        var literal = Assert.IsType<LiteralValue>(
            Assert.IsType<SlangValueOperand>(Assert.Single(trueBinding.Instruction.Operands)).Value);
        Assert.True(Assert.IsType<BoolLiteral>(literal.Value).Value);

        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var source = Emit(target);
            var dump = target.PrettyPrint();

            Assert.Contains(" = true;", source);
            Assert.Contains(" = false;", source);
            Assert.Contains(" = 1.5;", source);
            Assert.Contains("literal (true_b)", dump);
            Assert.Contains("literal (false_b)", dump);
            Assert.Contains("literal (1.5_f32)", dump);
            Assert.DoesNotContain("True", source);
            Assert.DoesNotContain("False", source);
            Assert.DoesNotContain("1,5", source);
            Assert.DoesNotContain("1,5", dump);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public async Task LogicalNotAndUnitCallUseTargetOperationSyntax()
    {
        var notEntry = Label.Create("not-entry");
        var input = new ParameterDeclaration("input", ShaderType.Bool, []);
        var loaded = ShaderValue.Intermediate(ShaderType.Bool);
        var negated = ShaderValue.Intermediate(ShaderType.Bool);
        var notDeclaration = new FunctionDeclaration(
            "Negate", [input], new FunctionReturn(ShaderType.Bool, []), []);
        var notBody = new FunctionBody4(
            notDeclaration,
            RegionTree.Block(notEntry, [], ShaderRegionBody.Create(
                notEntry,
                [],
                [
                    Instruction.Factory.Load(default, new LoadOperation(), loaded, input.Value),
                    Instruction.Factory.Operation1(
                        default,
                        LogicalNotOperation.Instance,
                        negated,
                        loaded)
                ],
                Terms.ReturnExpr(negated),
                null), null));
        var notSource = Emit(Lower(notBody));

        Assert.Contains(" = !v_", notSource);
        Assert.DoesNotContain("not(", notSource);
        await new SlangService().ValidateAsync(notSource);

        var observeEntry = Label.Create("observe-entry");
        var callerEntry = Label.Create("caller-entry");
        var observed = new ParameterDeclaration("value", ShaderType.I32, []);
        var callerInput = new ParameterDeclaration("value", ShaderType.I32, []);
        var observe = new FunctionDeclaration(
            "Observe", [observed], new FunctionReturn(ShaderType.Unit, []), []);
        var caller = new FunctionDeclaration(
            "CallObserve", [callerInput], new FunctionReturn(ShaderType.I32, []), []);
        var callerValue = ShaderValue.Intermediate(ShaderType.I32);
        var unitResult = ShaderValue.Intermediate(ShaderType.Unit);
        var observeBody = new FunctionBody4(
            observe,
            RegionTree.Block(observeEntry, [], ShaderRegionBody.Create(
                observeEntry, [], [], Terms.ReturnVoid(), null), null));
        var callerBody = new FunctionBody4(
            caller,
            RegionTree.Block(callerEntry, [], ShaderRegionBody.Create(
                callerEntry,
                [],
                [
                    Instruction.Factory.Load(
                        default,
                        new LoadOperation(),
                        callerValue,
                        callerInput.Value),
                    Instruction.Factory.Call(
                        default,
                        new CallOperation(Assert.IsType<FunctionType>(observe.Type)),
                        unitResult,
                        observe,
                        [callerValue])
                ],
                Terms.ReturnExpr(callerValue),
                null), null));
        var module = new ShaderModuleDeclaration<FunctionBody4>(
            [observe, caller],
            ImmutableDictionary<FunctionDeclaration, FunctionBody4>.Empty
                .Add(observe, observeBody)
                .Add(caller, callerBody));
        var target = new SlangTargetLowering().Lower(module);
        var effect = Assert.Single(
            Statements(target.GetBody(caller).Body).OfType<SlangEffect>(),
            statement => statement.Instruction.Operation is CallOperation);
        Assert.IsType<UnitType>(effect.Instruction.Result!.Type);
        var source = new SlangEmitter(target).Emit();

        Assert.Contains("void Observe(", source);
        Assert.Contains("Observe(v_", source);
        Assert.DoesNotContain(": Unit =", source);
        await new SlangService().ValidateAsync(source);
    }

    [Fact]
    public void UnsupportedAccessChainsAndScalarZeroConstructionFailInLowering()
    {
        var entry = Label.Create("entry");
        var declaration = Function("UnsupportedOperations", ShaderType.Unit);
        var local = new VariableDeclaration(FunctionAddressSpace.Instance, "value", ShaderType.I32, []);
        var accessResult = ShaderValue.Intermediate(ShaderType.I32.GetPtrType());
        var access = Instruction<IShaderValue, IShaderValue>.Create(
            new AccessChainOperation(), accessResult, [local.Value, Int(0)]);
        var accessBody = new FunctionBody4(
            declaration,
            RegionTree.Block(entry, [], ShaderRegionBody.Create(
                entry, [], [access], Terms.ReturnVoid(), null), null));
        Assert.Contains("access chains are not supported",
            Assert.Throws<NotSupportedException>(() => Lower(accessBody)).Message);

        var zeroResult = ShaderValue.Intermediate(ShaderType.I32);
        var zero = Instruction.Factory.ZeroConstructorOperation(
            default, new ZeroConstructorOperation(ShaderType.I32), zeroResult);
        var zeroBody = new FunctionBody4(
            declaration,
            RegionTree.Block(entry, [], ShaderRegionBody.Create(
                entry, [], [zero], Terms.ReturnVoid(), null), null));
        Assert.Contains("zero construction of i32",
            Assert.Throws<NotSupportedException>(() => Lower(zeroBody)).Message);
    }

    [Fact]
    public async Task RepresentativeControlFixturesCompileAndCanBeCaptured()
    {
        var fixtures = new (string Name, System.Reflection.MethodInfo Method)[]
        {
            ("ordinary-zero-loop", ((Func<int, int>)DevelopTestShaderModule.MinimumLoop).Method),
            ("early-return", ((Func<int, int, int, int>)ScalarControlFlowFixtures.NestedEarlyReturn).Method),
            ("nested-outer-transfer", ((Func<int, int, int>)ScalarControlFlowFixtures.NestedLoopControl).Method),
            ("cyclic-swap", ((Func<int, int>)ScalarControlFlowFixtures.LoopCarriedSwap).Method)
        };

        foreach (var (name, method) in fixtures)
        {
            var fixture = Lower(method);
            Capture(name, fixture.Region, fixture.Target.PrettyPrint(), fixture.Slang);
            await new SlangService().ValidateAsync(fixture.Slang);
        }
    }

    [Fact]
    public void EveryOriginalRegionHasExactlyOneProvenanceSite()
    {
        var fixture = Lower(((Func<int, int, int>)ScalarControlFlowFixtures.NestedLoopControl).Method);
        var sites = Statements(fixture.Target.Body)
            .Select(statement => statement switch
            {
                SlangScope { OriginalLabel: { } label } => label,
                SlangLoop { OriginalLabel: { } label } => label,
                _ => null
            })
            .OfType<Label>()
            .ToArray();

        Assert.Equal(fixture.Region.Control.Labels.Length, sites.Length);
        Assert.True(fixture.Region.Control.Labels.ToHashSet().SetEquals(sites));
        Assert.Contains(
            Statements(fixture.Target.Body).OfType<SlangLoop>(),
            loop => loop.OriginalLabel is null);
    }

    [Fact]
    public void NoChildRepeatLoopHasNoFabricatedReturn()
    {
        var loop = Label.Create("loop");
        var declaration = Function("Forever", ShaderType.I32);
        var body = new FunctionBody4(
            declaration,
            RegionTree.Loop(
                loop,
                [],
                ShaderRegionBody.Create(loop, [], [], Terms.Br(new(loop, [])), null),
                null,
                null));
        var target = Lower(body);
        var source = Emit(target);

        Assert.DoesNotContain("return", source);
        Assert.Single(
            Statements(target.Body).OfType<SlangLoop>(),
            statement => statement.OriginalLabel == loop);
        Assert.Throws<InvalidOperationException>(
            () => new EmittedScalarProgram(body, source).Run([], 100));
        Capture("nonterminating-repeat", body, target.PrettyPrint(), source);
        CaptureText(
            "nonterminating-repeat.execution.txt",
            "arguments=[] emitted=step budget exhausted; no fabricated return" + Environment.NewLine);
    }

    private LoweredFixture Lower(System.Reflection.MethodInfo method)
    {
        var region = new StablePointerRegionParameterPass().VisitFunctionBody(
            new FunctionToOperationPass().VisitFunctionBody(CompilerTestPipeline.CompileBody(method)));
        var target = Lower(region);
        var module = Module(target);
        var slang = new SlangEmitter(module).Emit();
        output.WriteLine(region.Dump());
        output.WriteLine(target.PrettyPrint());
        output.WriteLine(slang);
        return new LoweredFixture(region, target, module, slang);
    }

    private static SlangFunctionBody Lower(FunctionBody4 body) =>
        new SlangTargetLowering().Lower(Module(body)).GetBody(body.Declaration);

    private static ShaderModuleDeclaration<FunctionBody4> Module(FunctionBody4 body) =>
        new(
            [body.Declaration],
            ImmutableDictionary<FunctionDeclaration, FunctionBody4>.Empty.Add(body.Declaration, body));

    private static ShaderModuleDeclaration<SlangFunctionBody> Module(SlangFunctionBody body) =>
        new(
            [body.Declaration],
            ImmutableDictionary<FunctionDeclaration, SlangFunctionBody>.Empty.Add(body.Declaration, body));

    private static string Emit(SlangFunctionBody body) => new SlangEmitter(Module(body)).Emit();

    private static async Task<Execution> AssertEmittedEquivalent(
        FunctionBody4 body,
        ImmutableArray<Value> arguments,
        Value expected)
    {
        var source = Emit(Lower(body));
        var cfg = RunCfg(body, arguments);
        var emitted = new EmittedScalarProgram(body, source).Run(arguments);

        Assert.Equal(expected, cfg.Result);
        Assert.Equal(cfg.Result, emitted.Result);
        Assert.True(cfg.Trace.SequenceEqual(emitted.Trace),
            $"CFG trace: {string.Join(" -> ", cfg.Trace)}\n" +
            $"Emitted trace: {string.Join(" -> ", emitted.Trace)}");
        await new SlangService().ValidateAsync(source);
        return emitted;
    }

    private static FunctionDeclaration Function(string name, IShaderType returnType) =>
        new(name, [], new FunctionReturn(returnType, []), []);

    private static IShaderValue Int(int value) => ShaderValue.Literal(new I32Literal(value));

    private static IEnumerable<SlangStatement> Statements(SlangBlock block)
    {
        foreach (var statement in block.Statements)
        {
            yield return statement;
            switch (statement)
            {
                case SlangScope scope:
                    foreach (var nested in Statements(scope.Body)) yield return nested;
                    break;
                case SlangIf conditional:
                    foreach (var nested in Statements(conditional.WhenTrue)) yield return nested;
                    foreach (var nested in Statements(conditional.WhenFalse)) yield return nested;
                    break;
                case SlangLoop loop:
                    foreach (var nested in Statements(loop.Body)) yield return nested;
                    break;
            }
        }
    }

    private static void Capture(string name, FunctionBody4 region, string ast, string slang)
    {
        var directory = Environment.GetEnvironmentVariable("DRILLA_E2_CAPTURE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, $"{name}.region.txt"), region.Dump());
        File.WriteAllText(Path.Combine(directory, $"{name}.ast.txt"), ast);
        File.WriteAllText(Path.Combine(directory, $"{name}.slang"), slang);
    }

    private static void CaptureText(string name, string content)
    {
        var directory = Environment.GetEnvironmentVariable("DRILLA_E2_CAPTURE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, name), content);
    }

    private sealed record LoweredFixture(
        FunctionBody4 Region,
        SlangFunctionBody Target,
        ShaderModuleDeclaration<SlangFunctionBody> Module,
        string Slang);
}

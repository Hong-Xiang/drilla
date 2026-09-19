using System.Collections.Immutable;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Transform;
using DualDrill.CLSL.Language.Types;
using Xunit.Abstractions;

namespace DualDrill.CLSL.Test;

public class RegionParameterToLocalVariablePassTests(ITestOutputHelper output)
{
    private readonly ParameterDeclaration source = new("source", ShaderType.I32, []);
    private readonly IShaderValue condition = ShaderValue.Intermediate(ShaderType.Bool);
    private static readonly ITerminatorSemantic<RegionJump<IShaderValue>, IShaderValue,
            ITerminator<RegionJump<IShaderValue>, IShaderValue>>
        Terminators = Terminator.Factory<RegionJump<IShaderValue>, IShaderValue>();

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(8)]
    [InlineData(32)]
    public void AliasChainsResolveInLexicallyScopedOrder(int length)
    {
        var entry = Label.Create("entry");
        var labels = Enumerable.Range(0, length).Select(Label.FromIndex).ToArray();
        var pointers = labels.Select(_ => ShaderValue.Intermediate(source.Value.Type)).ToArray();
        var blocks = labels.Select((label, i) => Block(label, [pointers[i]],
            i + 1 < length ? Jump(labels[i + 1], pointers[i]) : Terminators.ReturnExpr(pointers[i])))
            .ToArray();
        var first = Block(entry, [], Jump(labels[0], source.Value));

        var result = Lower(CreateBody([first, .. blocks.Reverse()]));
        var returned = Assert.IsType<Terminator.D.ReturnExpr<RegionJump<IShaderValue>, IShaderValue>>(
            result[labels[^1]].Body.Last);
        Assert.Same(source.Value, returned.Expr);
        AssertLowered(result);
    }

    [Fact]
    public void SameAddressDiamondRewritesLoadsAndStoresWithoutCopyingPointee()
    {
        var local = new VariableDeclaration(FunctionAddressSpace.Instance, "local", ShaderType.I32, []);
        var entry = Label.Create("entry");
        var left = Label.Create("left");
        var right = Label.Create("right");
        var join = Label.Create("join");
        var pointer = ShaderValue.Intermediate(local.Value.Type);
        var loaded = ShaderValue.Intermediate(ShaderType.I32);
        var result = Lower(CreateBody([
            Block(entry, [], Terminators.BrIf(condition, new(left, []), new(right, []))),
            ShaderRegionBody.Create(join, [pointer], [
                Instruction.Factory.Load(default, new LoadOperation(), loaded, pointer),
                Instruction.Factory.Store(default, new StoreOperation(), pointer, loaded)
            ], Terminators.ReturnExpr(loaded), null),
            Block(left, [], Jump(join, local.Value)),
            Block(right, [], Jump(join, local.Value))
        ]));

        var instructions = result[join].Body.Elements.ToArray();
        Assert.Equal(2, instructions.Length);
        Assert.Same(local.Value, instructions[0].Operand0);
        Assert.Same(local.Value, instructions[1].Operand0);
        Assert.Same(loaded, instructions[0].Result);
        Assert.Same(local, Assert.Single(result.LocalVariables));
        AssertLowered(result);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(8)]
    public void RootedCyclesResolveToTheirExternalAddress(int length)
    {
        var entry = Label.Create("entry");
        var labels = Enumerable.Range(0, length).Select(Label.FromIndex).ToArray();
        var pointers = labels.Select(_ => ShaderValue.Intermediate(source.Value.Type)).ToArray();
        var blocks = labels.Select((label, i) =>
            ShaderRegionBody.Create(label, [pointers[i]], [
                Instruction.Factory.Load(default, new LoadOperation(),
                    ShaderValue.Intermediate(ShaderType.I32), pointers[i])
            ], Jump(labels[(i + 1) % length], pointers[i]), null)).ToArray();
        var first = Block(entry, [], Jump(labels[0], source.Value));
        var nested = RegionTree<Label, ShaderRegionBody>.Block(entry, [
            RegionTree<Label, ShaderRegionBody>.Loop(labels[0],
                [.. blocks.Skip(1).Reverse().Select(Region)], blocks[0], null, null)
        ], first, null);
        var declaration = new FunctionDeclaration("test", [source], new FunctionReturn(ShaderType.I32, []), []);
        var result = Lower(new FunctionBody4(declaration, nested));

        AssertLowered(result);
        Assert.Empty(result.LocalVariables);
        foreach (var label in labels)
            Assert.Same(source.Value, Assert.Single(result[label].Body.Elements).Operand0);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void RootlessCyclesAreRejected(int length)
    {
        var entry = Label.Create("entry");
        var labels = Enumerable.Range(0, length).Select(Label.FromIndex).ToArray();
        var pointers = labels.Select(_ => ShaderValue.Intermediate(source.Value.Type)).ToArray();
        var blocks = labels.Select((label, i) =>
            Block(label, [pointers[i]], Jump(labels[(i + 1) % length], pointers[i]))).ToArray();
        var entryBody = Block(entry, [], Jump(labels[0], pointers[0]));
        var tree = RegionTree<Label, ShaderRegionBody>.Block(entry, [
            RegionTree<Label, ShaderRegionBody>.Loop(labels[0],
                [.. blocks.Skip(1).Reverse().Select(Region)], blocks[0], null, null)
        ], entryBody, null);
        var body = new FunctionBody4(
            new FunctionDeclaration("test", [source], new FunctionReturn(UnitType.Instance, []), []),
            tree);

        var error = Assert.Throws<NotSupportedException>(() => Lower(body));

        Assert.Contains("no stable address", error.Message);
        output.WriteLine($"ACTUAL resolver rejection: {error.Message}");
    }

    [Fact]
    public void DifferentAddressesInCycleAreRejectedEvenWhenDiscoveredLate()
    {
        var other = new ParameterDeclaration("source", ShaderType.I32, []);
        var entry = Label.Create("entry");
        var loop = Label.Create("loop");
        var back = Label.Create("back");
        var p = ShaderValue.Intermediate(source.Value.Type);
        var q = ShaderValue.Intermediate(source.Value.Type);
        var entryBody = Block(entry, [], Jump(loop, source.Value));
        var loopBody = Block(loop, [p], Jump(back, other.Value));
        var backBody = Block(back, [q], Jump(loop, q));
        var body = new FunctionBody4(
            new FunctionDeclaration("test", [source, other], new FunctionReturn(UnitType.Instance, []), []),
            RegionTree<Label, ShaderRegionBody>.Block(entry, [
                RegionTree<Label, ShaderRegionBody>.Loop(loop, [
                    Region(backBody)
                ], loopBody, null, null)
            ], entryBody, null));

        var error = Assert.Throws<NotSupportedException>(() => Lower(body));

        Assert.Contains("multiple stable addresses", error.Message);
    }

    [Fact]
    public void StableRootDoesNotHideAnUngroundedDependency()
    {
        var entry = Label.Create("entry");
        var join = Label.Create("join");
        var orphan = Label.Create("orphan");
        var p = ShaderValue.Intermediate(source.Value.Type);
        var q = ShaderValue.Intermediate(source.Value.Type);
        var body = new FunctionBody4(
            new FunctionDeclaration("test", [source], new FunctionReturn(source.Value.Type, []), []),
            RegionTree<Label, ShaderRegionBody>.Block(entry, [
                Region(Block(join, [p], Terminators.ReturnExpr(p))),
                RegionTree<Label, ShaderRegionBody>.Loop(orphan, [],
                    Block(orphan, [q],
                        Terminators.BrIf(condition, new(orphan, [q]), new(join, [q]))),
                    null,
                    null)
            ], Block(entry, [],
                Terminators.BrIf(condition, new(join, [source.Value]), new(orphan, [q]))), null));

        var error = Assert.Throws<NotSupportedException>(() => Lower(body));

        Assert.Contains("no stable address", error.Message);
        output.WriteLine($"ACTUAL resolver rejection: {error.Message}");
    }

    [Fact]
    public void ComputedPointerIsRejected()
    {
        var entry = Label.Create("entry");
        var target = Label.Create("target");
        var computed = ShaderValue.Intermediate(source.Value.Type);
        var parameter = ShaderValue.Intermediate(source.Value.Type);
        var body = CreateBody([
            Block(entry, [], Terminators.BrIf(condition, new(target, [source.Value]), new(target, [computed]))),
            Block(target, [parameter], Terminators.ReturnExpr(parameter))
        ]);

        var error = Assert.Throws<NotSupportedException>(() => Lower(body));

        Assert.Contains("unsupported pointer source", error.Message);
    }

    [Fact]
    public void PointerWithoutIncomingEdgeIsRejectedEvenIfUnused()
    {
        var entry = Label.Create("entry");
        var error = Assert.Throws<ArgumentException>(() => CreateBody([
            Block(entry, [ShaderValue.Intermediate(source.Value.Type)], Terminators.ReturnVoid())
        ]));

        Assert.Contains("entry region", error.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void WrongJumpArityIsRejected(int argumentCount)
    {
        var entry = Label.Create("entry");
        var target = Label.Create("target");
        var error = Assert.Throws<ArgumentException>(() => CreateBody([
            Block(entry, [], Jump(target, [.. Enumerable.Repeat<IShaderValue>(source.Value, argumentCount)])),
            Block(target, [ShaderValue.Intermediate(source.Value.Type)], Terminators.ReturnVoid())
        ]));

        Assert.Contains("argument count", error.Message);
    }

    [Fact]
    public void PointerAddressSpaceMismatchIsRejected()
    {
        var entry = Label.Create("entry");
        var target = Label.Create("target");
        var inputPointer = ShaderValue.Intermediate(ShaderType.I32.GetPtrType(InputAddressSpace.Instance));
        var error = Assert.Throws<ArgumentException>(() => CreateBody([
            Block(entry, [], Jump(target, source.Value)),
            Block(target, [inputPointer], Terminators.ReturnVoid())
        ]));

        Assert.Contains("argument type", error.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void ValueOnlyJumpArityIsChecked(int argumentCount)
    {
        var entry = Label.Create("entry");
        var target = Label.Create("target");
        var value = ShaderValue.Intermediate(ShaderType.I32);
        Assert.Throws<ArgumentException>(() => CreateBody([
            Block(entry, [], Jump(target, [.. Enumerable.Repeat<IShaderValue>(value, argumentCount)])),
            Block(target, [ShaderValue.Intermediate(ShaderType.I32)], Terminators.ReturnVoid())
        ]));
    }

    [Fact]
    public void ValueOnlyJumpTypeIsChecked()
    {
        var entry = Label.Create("entry");
        var target = Label.Create("target");
        Assert.Throws<ArgumentException>(() => CreateBody([
            Block(entry, [], Jump(target, condition)),
            Block(target, [ShaderValue.Intermediate(ShaderType.I32)], Terminators.ReturnVoid())
        ]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ValueLoweringPreservesSlotsAndIsIdempotent(bool includePointer)
    {
        var entry = Label.Create("entry");
        var target = Label.Create("target");
        var pointer = ShaderValue.Intermediate(source.Value.Type);
        var value = ShaderValue.Intermediate(ShaderType.I32);
        var argument = ShaderValue.Intermediate(ShaderType.I32);
        var body = CreateBody([
            Block(entry, [], Jump(target, includePointer ? [source.Value, argument] : [argument])),
            Block(target, includePointer ? [pointer, value] : [value], Terminators.ReturnExpr(value))
        ]);

        var result = Lower(body);
        AssertLowered(result);
        var store = Assert.Single(result[entry].Body.Elements);
        var load = Assert.Single(result[target].Body.Elements);
        Assert.IsType<StoreOperation>(store.Operation);
        Assert.IsType<LoadOperation>(load.Operation);
        Assert.Same(argument, store.Operand1);
        Assert.Same(store.Operand0, load.Operand0);
        Assert.Same(load.Result,
            Assert.IsType<Terminator.D.ReturnExpr<RegionJump<IShaderValue>, IShaderValue>>(
                result[target].Body.Last).Expr);
        Assert.Single(result.LocalVariables);

        var twice = Lower(result);
        AssertLowered(twice);
        Assert.Equal(result[entry].Body.Elements, twice[entry].Body.Elements);
        Assert.Equal(result[target].Body.Elements, twice[target].Body.Elements);
        Assert.Equal(result.LocalVariables, twice.LocalVariables);
        Assert.Single(body[target].Parameters.Where(p => p.Type is not IPtrType));
        Assert.NotEmpty(Assert.IsType<Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue>>(body[entry].Body.Last)
            .Target.Arguments);
    }

    private static FunctionBody4 Lower(FunctionBody4 body) =>
        new RegionParameterToLocalVariablePass().VisitFunctionBody(body);

    [Fact]
    public void PointerOnlyPassPreservesOrdinaryParametersAndSelectedArguments()
    {
        var entry = Label.Create("entry");
        var target = Label.Create("target");
        var pointer = ShaderValue.Intermediate(source.Value.Type);
        var value = ShaderValue.Intermediate(ShaderType.I32);
        var left = ShaderValue.Intermediate(ShaderType.I32);
        var right = ShaderValue.Intermediate(ShaderType.I32);
        var body = CreateBody([
            Block(entry, [], Terminators.BrIf(
                condition,
                new(target, [source.Value, left]),
                new(target, [source.Value, right]))),
            Block(target, [pointer, value], Terminators.ReturnExpr(value))
        ]);

        var result = new StablePointerRegionParameterPass().VisitFunctionBody(body);
        var branch = Assert.IsType<Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue>>(
            result[entry].Body.Last);

        Assert.Same(value, Assert.Single(result[target].Parameters));
        Assert.Same(left, Assert.Single(branch.TrueTarget.Arguments));
        Assert.Same(right, Assert.Single(branch.FalseTarget.Arguments));
    }

    [Fact]
    public void InterleavedParametersPreserveParallelCopiesAndAllCallOperands()
    {
        var entry = Label.Create("entry");
        var loop = Label.Create("loop");
        var exit = Label.Create("exit");
        var p = ShaderValue.Intermediate(source.Value.Type);
        var q = ShaderValue.Intermediate(source.Value.Type);
        var a = ShaderValue.Intermediate(ShaderType.I32);
        var b = ShaderValue.Intermediate(ShaderType.I32);
        var first = ShaderValue.Intermediate(ShaderType.I32);
        var second = ShaderValue.Intermediate(ShaderType.I32);
        var callResult = ShaderValue.Intermediate(ShaderType.I32);
        var callee = new FunctionDeclaration("callee", [
            new ParameterDeclaration("p", source.Value.Type, []),
            new ParameterDeclaration("q", source.Value.Type, [])
        ], new FunctionReturn(ShaderType.I32, []), []);
        var entryBody = Block(entry, [], Jump(loop, source.Value, first, source.Value, second));
        var exitBody = Block(exit, [], Terminators.ReturnExpr(callResult));
        var loopBody = ShaderRegionBody.Create(loop, [p, a, q, b], [
                Instruction.Factory.Call(default, new CallOperation(Assert.IsType<FunctionType>(callee.Type)),
                    callResult, callee, [p, q])
            ], Terminators.BrIf(condition, new(loop, [p, b, q, a]), new(exit, [])), null);
        var body = new FunctionBody4(
            new FunctionDeclaration("test", [source], new FunctionReturn(ShaderType.I32, []), []),
            RegionTree<Label, ShaderRegionBody>.Block(entry, [
                Region(exitBody),
                RegionTree<Label, ShaderRegionBody>.Loop(loop, [], loopBody, null, null)
            ], entryBody, null));

        var result = Lower(body);
        AssertLowered(result);
        Assert.Equal(2, result.LocalVariables.Length);
        var initialStores = result[entry].Body.Elements.ToArray();
        var instructions = result[loop].Body.Elements.ToArray();
        Assert.Equal(2, initialStores.Length);
        Assert.Equal(5, instructions.Length);
        Assert.Same(first, initialStores[0].Operand1);
        Assert.Same(second, initialStores[1].Operand1);
        Assert.Same(initialStores[0].Operand0, instructions[0].Operand0);
        Assert.Same(initialStores[1].Operand0, instructions[1].Operand0);
        Assert.Same(callee, instructions[2].Operand0);
        Assert.Same(source.Value, instructions[2].Operand1);
        Assert.Same(source.Value, Assert.Single(instructions[2].RestOperands));
        Assert.Same(callResult, instructions[2].Result);
        Assert.Same(instructions[0].Operand0, instructions[3].Operand0);
        Assert.Same(instructions[1].Operand0, instructions[4].Operand0);
        Assert.Same(instructions[1].Result, instructions[3].Operand1);
        Assert.Same(instructions[0].Result, instructions[4].Operand1);
    }

    [Fact]
    public void DifferentValueArgumentsToSharedTargetAreRejected()
    {
        var entry = Label.Create("entry");
        var target = Label.Create("target");
        var a = ShaderValue.Intermediate(ShaderType.I32);
        var b = ShaderValue.Intermediate(ShaderType.I32);
        var parameter = ShaderValue.Intermediate(ShaderType.I32);
        var body = CreateBody([
            Block(entry, [], Terminators.BrIf(condition, new(target, [a]), new(target, [b]))),
            Block(target, [parameter], Terminators.ReturnExpr(parameter))
        ]);

        var error = Assert.Throws<NotSupportedException>(() => Lower(body));

        Assert.Contains("edge-specific lowering", error.Message);
    }

    [Fact]
    public void IdenticalValueArgumentsToSharedTargetAreStoredOnce()
    {
        var entry = Label.Create("entry");
        var target = Label.Create("target");
        var value = ShaderValue.Intermediate(ShaderType.I32);
        var parameter = ShaderValue.Intermediate(ShaderType.I32);
        var body = CreateBody([
            Block(entry, [], Terminators.BrIf(condition, new(target, [value]), new(target, [value]))),
            Block(target, [parameter], Terminators.ReturnExpr(parameter))
        ]);

        var result = Lower(body);

        AssertLowered(result);
        Assert.Same(value, Assert.Single(result[entry].Body.Elements).Operand1);
    }

    private FunctionBody4 CreateBody(ShaderRegionBody[] blocks) =>
        new(new FunctionDeclaration("test", [source],
                new FunctionReturn(blocks.Select(b => b.Body.Last)
                    .OfType<Terminator.D.ReturnExpr<RegionJump<IShaderValue>, IShaderValue>>()
                    .Select(t => t.Expr.Type).FirstOrDefault(UnitType.Instance), []), []),
            RegionTree<Label, ShaderRegionBody>.Block(blocks[0].Label,
                [.. blocks.Skip(1).Select(Region)], blocks[0], null));

    private static RegionTree<Label, ShaderRegionBody> Region(ShaderRegionBody block) =>
        RegionTree<Label, ShaderRegionBody>.Block(block.Label, [], block, null);

    private static ShaderRegionBody Block(Label label, ImmutableArray<IShaderValue> parameters,
        ITerminator<RegionJump<IShaderValue>, IShaderValue> terminator) =>
        ShaderRegionBody.Create(label, parameters, [], terminator, null);

    private static ITerminator<RegionJump<IShaderValue>, IShaderValue> Jump(Label target,
        params IShaderValue[] arguments) =>
        Terminators.Br(new RegionJump<IShaderValue>(target, [.. arguments]));

    private static void AssertLowered(FunctionBody4 body)
    {
        body.Body.Traverse((_, _, block) =>
        {
            Assert.Empty(block.Parameters);
            block.Body.Last.Select(jump =>
            {
                Assert.Empty(jump.Arguments);
                return jump;
            }, static value => value);
            return false;
        });
        Assert.DoesNotContain(body.LocalVariables, variable => variable.Type is IPtrType);
    }
}

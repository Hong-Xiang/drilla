using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Globalization;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.CodeTextWriter;
using Xunit.Abstractions;
using static DualDrill.CLSL.Test.ScalarControlFlowOracle;
using static DualDrill.CLSL.Test.ScopedContinuationOracle;

namespace DualDrill.CLSL.Test;

public sealed class ScopedRegionControlTests(ITestOutputHelper output)
{
    private static readonly ITerminatorSemantic<RegionJump<IShaderValue>, IShaderValue,
            ITerminator<RegionJump<IShaderValue>, IShaderValue>>
        Terms = Terminator.Factory<RegionJump<IShaderValue>, IShaderValue>();

    [Fact]
    public void SameTargetArmsKeepDistinctTuplesAndExecuteSharedEffectOnce()
    {
        var body = ScalarControlFlowTests.HandBody();

        foreach (var (input, expected) in new[] { (1, 11), (-1, 21) })
        {
            var arguments = ImmutableArray.Create<Value>(new Value.Integer(input));
            var cfg = RunCfg(body, arguments);
            var scoped = RunScoped(body, arguments);

            Assert.Equal(new Value.Integer(expected), scoped.Result);
            Assert.Equal(cfg.Result, scoped.Result);
            Assert.True(cfg.Trace.SequenceEqual(scoped.Trace));
            output.WriteLine(
                $"ACTUAL input={input} result={scoped.Result} trace={string.Join(" -> ", scoped.Trace.Select(LabelName))}");
        }

        var entryTransfers = body.Control.Transfers.Where(transfer => ReferenceEquals(transfer.Source, body.Entry));
        Assert.Equal([0, 1], entryTransfers.Select(transfer => transfer.Arm));
        Assert.All(entryTransfers, transfer => Assert.Equal("tail", transfer.Target.Name));
        Assert.Equal(
            """
            scope entry: [exit forward owner=entry, tail forward owner=entry]
            scope exit: []
            scope tail: [exit forward owner=entry]
            transfer entry#0 -> tail forward owner=entry
            transfer entry#1 -> tail forward owner=entry
            transfer tail#0 -> exit forward owner=entry

            """,
            FormatControl(body));
        output.WriteLine("ACTUAL scoped control:");
        output.WriteLine(body.Dump());
    }

    [Fact]
    public void CrossScopeTransfersRetainTheirDefiningOwners()
    {
        var entry = Label.Create("entry");
        var outer = Label.Create("outer");
        var inner = Label.Create("inner");
        var afterInner = Label.Create("after-inner");
        var done = Label.Create("done");
        var condition = ShaderValue.Literal(new BoolLiteral(false));
        var body = Function(
            RegionTree.Block(entry, [
                RegionTree.Block(done, [], Body(done,
                    Terms.ReturnExpr(ShaderValue.Literal(new I32Literal(0)))), null),
                RegionTree.Loop(outer, [
                    RegionTree.Block(afterInner, [], Body(afterInner, Terms.Br(new(done, []))), null),
                    RegionTree.Loop(inner, [], Body(inner,
                        Terms.BrIf(condition, new(outer, []), new(afterInner, []))), null, null)
                ], Body(outer, Terms.Br(new(inner, []))), null, null)
            ], Body(entry, Terms.Br(new(outer, []))), null),
            ShaderType.I32);

        var repeat = body.Control.Resolve(inner, 0);
        var innerExit = body.Control.Resolve(inner, 1);
        var outerExit = body.Control.Resolve(afterInner, 0);

        Assert.Equal(ScopedContinuationKind.Repeat, repeat.Kind);
        Assert.Same(outer, repeat.Owner);
        Assert.Equal(ScopedContinuationKind.Forward, innerExit.Kind);
        Assert.Same(outer, innerExit.Owner);
        Assert.Equal(ScopedContinuationKind.Forward, outerExit.Kind);
        Assert.Same(entry, outerExit.Owner);
        Assert.True(new[] { entry, outer, inner, afterInner, done }
            .SequenceEqual(RunScoped(body, []).Trace));
    }

    [Fact]
    public void CheckedBoundaryRejectsInvalidScopedBodies()
    {
        var entry = Label.Create("entry");
        var first = Label.Create("first");
        var second = Label.Create("second");
        var privateLabel = Label.Create("private");
        var unknown = Label.Create("unknown");
        var parameter = ShaderValue.Intermediate(ShaderType.I32);

        Rejects("not visible", RegionTree.Block(entry, [
            RegionTree.Block(first, [], Body(first, Terms.Br(new(second, []))), null),
            RegionTree.Block(second, [], Body(second, Terms.ReturnVoid()), null)
        ], Body(entry, Terms.Br(new(first, []))), null));

        Rejects("not visible", RegionTree.Block(entry, [
            RegionTree.Block(first, [
                RegionTree.Block(privateLabel, [], Body(privateLabel, Terms.ReturnVoid()), null)
            ], Body(first, Terms.Br(new(privateLabel, []))), null),
            RegionTree.Block(second, [], Body(second, Terms.Br(new(privateLabel, []))), null)
        ], Body(entry, Terms.Br(new(second, []))), null));

        Rejects("block self-reference",
            RegionTree.Block(entry, [], Body(entry, Terms.Br(new(entry, []))), null));
        Rejects("unknown label",
            RegionTree.Block(entry, [], Body(entry, Terms.Br(new(unknown, []))), null));
        Rejects("duplicate defined label", RegionTree.Block(entry, [
            RegionTree.Block(entry, [], Body(entry, Terms.ReturnVoid()), null)
        ], Body(entry, Terms.ReturnVoid()), null));
        Rejects("does not match body label",
            RegionTree.Block(entry, [], Body(first, Terms.ReturnVoid()), null));
        Rejects("incorrect argument count", RegionTree.Block(entry, [
            RegionTree.Block(first, [], Body(first, Terms.ReturnVoid(), [parameter]), null)
        ], Body(entry, Terms.Br(new(first, []))), null));
        Rejects("incorrect argument type", RegionTree.Block(entry, [
            RegionTree.Block(first, [], Body(first, Terms.ReturnVoid(), [parameter]), null)
        ], Body(entry, Terms.Br(new(first, [ShaderValue.Literal(new BoolLiteral(true))]))), null));
        Rejects("entry region", RegionTree.Block(entry, [],
            Body(entry, Terms.ReturnVoid(), [parameter]), null));
        Rejects("unreachable definitions", RegionTree.Block(entry, [
            RegionTree.Block(first, [], Body(first, Terms.ReturnVoid()), null)
        ], Body(entry, Terms.ReturnVoid()), null));
    }

    [Fact]
    public void LoopSelfRepeatIsCheckedAndBudgeted()
    {
        var loop = Label.Create("loop");
        var body = Function(
            RegionTree.Loop(loop, [], Body(loop, Terms.Br(new(loop, []))), null, null),
            ShaderType.I32);

        var transfer = body.Control.Resolve(loop, 0);
        Assert.Equal(ScopedContinuationKind.Repeat, transfer.Kind);
        Assert.Same(loop, transfer.Owner);
        Assert.Contains("step budget", Assert.Throws<InvalidOperationException>(
            () => RunScoped(body, [], 8)).Message);
    }

    [Fact]
    public void StructurallyEquivalentPointerTypesAreAccepted()
    {
        var entry = Label.Create("entry");
        var target = Label.Create("target");
        var source = new ParameterDeclaration("source", ShaderType.I32, []);
        var parameter = ShaderValue.Intermediate(
            new PtrType(ShaderType.I32, FunctionAddressSpace.Instance));
        var body = new FunctionBody4(
            new FunctionDeclaration("Pointers", [source], new FunctionReturn(UnitType.Instance, []), []),
            RegionTree.Block(entry, [
                RegionTree.Block(target, [], Body(target, Terms.ReturnVoid(), [parameter]), null)
            ], Body(entry, Terms.Br(new(target, [source.Value]))), null));

        Assert.Same(target, body.Control.Resolve(entry, 0).Target);
    }

    [Fact]
    public void GenericRegionConstructionRejectsIrreducibleSiblingCycle()
    {
        var entry = Label.Create("entry");
        var left = Label.Create("left");
        var right = Label.Create("right");
        var graph = new ControlFlowGraph<ISuccessor>(
            entry,
            new Dictionary<Label, ControlFlowGraph<ISuccessor>.NodeDefinition>
            {
                [entry] = new(new ConditionalSuccessor(left, right), new ConditionalSuccessor(left, right)),
                [left] = new(new UnconditionalSuccessor(right), new UnconditionalSuccessor(right)),
                [right] = new(new UnconditionalSuccessor(left), new UnconditionalSuccessor(left))
            });
        var annotated = ControlFlowFacts.Annotate(
            graph,
            static (_, _, _, _) => { });

        var error = Assert.Throws<ArgumentException>(() =>
            RegionTree.Create(annotated, static (_, body, _) => body));

        Assert.Contains("lexical scope", error.Message);
    }

    private static string LabelName(Label label) => label.Name ?? "<unnamed>";

    private static string FormatControl(FunctionBody4 body)
    {
        using var text = new StringWriter(CultureInfo.InvariantCulture);
        using var writer = new IndentedTextWriter(text);
        body.Control.Dump(writer, LabelName);
        return text.ToString();
    }

    private static void Rejects(string expected, RegionTree<Label, ShaderRegionBody> tree)
    {
        var error = Assert.Throws<ArgumentException>(() => Function(tree));
        Assert.Contains(expected, error.Message);
    }

    private static FunctionBody4 Function(
        RegionTree<Label, ShaderRegionBody> tree,
        IShaderType? returnType = null) =>
        new(
            new FunctionDeclaration(
                "Scoped",
                [],
                new FunctionReturn(returnType ?? UnitType.Instance, []),
                []),
            tree);

    private static ShaderRegionBody Body(
        Label label,
        ITerminator<RegionJump<IShaderValue>, IShaderValue> terminator,
        ImmutableArray<IShaderValue> parameters = default) =>
        ShaderRegionBody.Create(label, parameters.IsDefault ? [] : parameters, [], terminator, null);
}

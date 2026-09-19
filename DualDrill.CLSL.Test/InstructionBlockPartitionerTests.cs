using System.CodeDom.Compiler;
using DualDrill.CLSL.Compiler;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Test;

public class InstructionBlockPartitionerTests
{
    public readonly record struct SimpleNode(Label Label, int Start, int Count, ISuccessor Successor) : ILabeledEntity;

    static SimpleNode CreateNode(
        Label label,
        InstructionBlockPartitioner.InstructionRange range,
        ISuccessor successor) =>
        new(label, range.Start, range.Count, successor);

    static ControlFlowGraph<SimpleNode> Build(InstructionBlockPartitioner builder) =>
        ControlFlowGraph.Create(
            builder.Build(CreateNode, static node => node.Successor, PrintBlocks),
            static node => node.Successor);

    static void PrintBlocks(
        BlockList<SimpleNode> blocks,
        IndentedTextWriter writer,
        PrettyPrintOption option)
    {
        foreach (var block in blocks.Blocks)
            writer.WriteLine(block);
    }

    [Fact]
    public void SimpleSingleNodeShouldWork()
    {
        var builder = new InstructionBlockPartitioner(3, Label.Create);

        var cfg = Build(builder);

        Assert.Equal(1, cfg.Count);

        var e = cfg.EntryLabel;
        Assert.IsType<TerminateSuccessor>(cfg.Successor(e));
        Assert.Empty(cfg.Predecessor(e));

        Assert.Equal(new SimpleNode(e, 0, 3, cfg.Successor(e)), cfg[e]);

        Assert.Equal([e], cfg.Labels());
    }

    [Fact]
    public void SingleSelfLoopNodeShouldWork()
    {
        var builder = new InstructionBlockPartitioner(3, Label.Create);

        builder.AddBr(2, 0);
        var cfg = Build(builder);

        Assert.Equal(1, cfg.Count);

        var e = cfg.EntryLabel;
        var se = Assert.IsType<UnconditionalSuccessor>(cfg.Successor(e));
        Assert.Equal(e, se.Target);
        Assert.Equal([e], cfg.Predecessor(e));

        Assert.Equal(new SimpleNode(e, 0, 3, cfg.Successor(e)), cfg[e]);

        Assert.Equal([e], cfg.Labels());
    }

    [Fact]
    public void TwoNodeChainShouldWork()
    {
        var builder = new InstructionBlockPartitioner(5, Label.Create);

        var n = builder.AddBr(2, 3);
        var cfg = Build(builder);

        var e = cfg.EntryLabel;
        Assert.Equal(2, cfg.Count);

        var se = Assert.IsType<UnconditionalSuccessor>(cfg.Successor(e));
        Assert.Equal(n, se.Target);
        Assert.Equal([], cfg.Predecessor(e));

        Assert.IsType<TerminateSuccessor>(cfg.Successor(n));
        Assert.Equal([e], cfg.Predecessor(n));

        Assert.Equal(new SimpleNode(e, 0, 3, cfg.Successor(e)), cfg[e]);
        Assert.Equal(new SimpleNode(n, 3, 2, cfg.Successor(n)), cfg[n]);


        Assert.Equal([e, n], cfg.Labels());
    }

    [Fact]
    public void PenultimateBlockShouldFallThroughToFinalInstruction()
    {
        var builder = new InstructionBlockPartitioner(3, Label.Create);

        var final = builder.AddBr(0, 2);
        var cfg = Build(builder);
        var penultimate = Assert.Single(cfg.Predecessor(final), label => label != cfg.EntryLabel);

        Assert.Equal(new SimpleNode(penultimate, 1, 1, cfg.Successor(penultimate)), cfg[penultimate]);
        Assert.Equal(final, Assert.IsType<UnconditionalSuccessor>(cfg.Successor(penultimate)).Target);
        Assert.Equal(new SimpleNode(final, 2, 1, cfg.Successor(final)), cfg[final]);
        Assert.IsType<TerminateSuccessor>(cfg.Successor(final));
    }

    [Fact]
    public void TwoNodeBrIfLoopShouldWork()
    {
        var builder = new InstructionBlockPartitioner(5, Label.Create);

        var e2 = builder.AddBrIf(2, 0);
        var cfg = Build(builder);

        Assert.Equal(2, cfg.Count);

        var e = cfg.EntryLabel;
        Assert.Equal(e2, e);

        var se = Assert.IsType<ConditionalSuccessor>(cfg.Successor(e));
        Assert.Equal(e, se.TrueTarget);
        var n = se.FalseTarget;
        Assert.Equal([e], cfg.Predecessor(e));

        Assert.IsType<TerminateSuccessor>(cfg.Successor(n));
        Assert.Equal([e], cfg.Predecessor(n));

        Assert.Equal(new SimpleNode(e, 0, 3, cfg.Successor(e)), cfg[e]);
        Assert.Equal(new SimpleNode(n, 3, 2, cfg.Successor(n)), cfg[n]);

        Assert.Equal([e, n], cfg.Labels());
    }

    [Fact]
    public void MinimumIfThenElseShouldWork()
    {
        var builder = new InstructionBlockPartitioner(10, Label.Create);
        // e : 0 - 2
        // f : 3 - 4
        // t : 5 - 7
        // m : 8 - 9

        var t = builder.AddBrIf(2, 5);
        var m = builder.AddBr(4, 8);

        var cfg = Build(builder);

        Assert.Equal(4, cfg.Count);

        var e = cfg.EntryLabel;
        var se = Assert.IsType<ConditionalSuccessor>(cfg.Successor(e));
        Assert.Equal(t, se.TrueTarget);
        var f = se.FalseTarget;
        Assert.Equal([], cfg.Predecessor(e));

        var st = Assert.IsType<UnconditionalSuccessor>(cfg.Successor(t));
        Assert.Equal(m, st.Target);
        Assert.Equal([e], cfg.Predecessor(t));

        var sf = Assert.IsType<UnconditionalSuccessor>(cfg.Successor(f));
        Assert.Equal(m, st.Target);
        Assert.Equal([e], cfg.Predecessor(f));

        Assert.IsType<TerminateSuccessor>(cfg.Successor(m));
        Assert.Contains(t, cfg.Predecessor(m));
        Assert.Contains(f, cfg.Predecessor(m));

        Assert.Equal(new SimpleNode(e, 0, 3, cfg.Successor(e)), cfg[e]);
        Assert.Equal(new SimpleNode(t, 5, 3, cfg.Successor(t)), cfg[t]);
        Assert.Equal(new SimpleNode(f, 3, 2, cfg.Successor(f)), cfg[f]);
        Assert.Equal(new SimpleNode(m, 8, 2, cfg.Successor(m)), cfg[m]);


        Assert.Equal([e, f, t, m], cfg.Labels());
    }

    [Fact]
    public void MinimumIfThenElseWithAddtionalBrShouldWork()
    {
        var builder = new InstructionBlockPartitioner(10, Label.Create);
        // e : 0 - 2
        // f : 3 - 4
        // t : 5 - 7
        // m : 8 - 9

        var t = builder.AddBrIf(2, 5);
        var m = builder.AddBr(4, 8);
        var m2 = builder.AddBr(7, 8);
        Assert.Equal(m, m2);

        var cfg = Build(builder);

        Assert.Equal(4, cfg.Count);
    }

    [Fact]
    public void ChainedLoop3NodeShouldWork()
    {
        var builder = new InstructionBlockPartitioner(9, Label.Create);
        // a : 0 - 2
        // b : 3 - 5
        // c : 6 - 8
        //
        // a -> b -> c
        // ^    |
        // | -- |
        var b = builder.AddBr(2, 3);
        var a = builder.AddBrIf(5, 0);

        var cfg = Build(builder);

        Assert.Equal(3, cfg.Count);

        Assert.Equal(a, cfg.EntryLabel);
        var sa = Assert.IsType<UnconditionalSuccessor>(cfg.Successor(a));
        Assert.Equal(b, sa.Target);
        Assert.Equal([b], cfg.Predecessor(a));


        var sb = Assert.IsType<ConditionalSuccessor>(cfg.Successor(b));
        var c = sb.FalseTarget;
        Assert.Equal(a, sb.TrueTarget);
        Assert.Equal([a], cfg.Predecessor(b));


        Assert.IsType<TerminateSuccessor>(cfg.Successor(c));
        Assert.Equal([b], cfg.Predecessor(c));

        Assert.Equal(new SimpleNode(a, 0, 3, cfg.Successor(a)), cfg[a]);
        Assert.Equal(new SimpleNode(b, 3, 3, cfg.Successor(b)), cfg[b]);
        Assert.Equal(new SimpleNode(c, 6, 3, cfg.Successor(c)), cfg[c]);

        Assert.Equal([a, b, c], cfg.Labels());
    }


    [Fact]
    public void ComplexMergeBranchShouldWork()
    {
        // a: 0 - 3, br.if d b
        // b: 4 - 6, br.if e c
        // c: 7 - 9, br f
        // d: 10 - 12, br.if f e
        // e: 13 - 15, (implicit br f)
        // f: 16 - 18

        var builder = new InstructionBlockPartitioner(19, Label.Create);

        var d = builder.AddBrIf(3, 10);
        var e = builder.AddBrIf(6, 13);
        var f = builder.AddBr(9, 16);
        var f2 = builder.AddBrIf(12, 16);
        Assert.Equal(f, f2);

        var cfg = Build(builder);

        Assert.Equal(6, cfg.Count);

        var a = cfg.EntryLabel;
        var sa = Assert.IsType<ConditionalSuccessor>(cfg.Successor(a));
        var b = sa.FalseTarget;
        var sb = Assert.IsType<ConditionalSuccessor>(cfg.Successor(b));
        var c = sb.FalseTarget;
        var sc = Assert.IsType<UnconditionalSuccessor>(cfg.Successor(c));
        var sd = Assert.IsType<ConditionalSuccessor>(cfg.Successor(d));
        var se = Assert.IsType<UnconditionalSuccessor>(cfg.Successor(e));
        var sf = Assert.IsType<TerminateSuccessor>(cfg.Successor(f));

        Assert.Equal(d, sa.TrueTarget);
        Assert.Equal(e, sb.TrueTarget);
        Assert.Equal(f, sc.Target);
        Assert.Equal(f, sd.TrueTarget);
        Assert.Equal(f, se.Target);

        Assert.Equal(new SimpleNode(a, 0, 4, cfg.Successor(a)), cfg[a]);
        Assert.Equal(new SimpleNode(b, 4, 3, cfg.Successor(b)), cfg[b]);
        Assert.Equal(new SimpleNode(c, 7, 3, cfg.Successor(c)), cfg[c]);
        Assert.Equal(new SimpleNode(d, 10, 3, cfg.Successor(d)), cfg[d]);
        Assert.Equal(new SimpleNode(e, 13, 3, cfg.Successor(e)), cfg[e]);
        Assert.Equal(new SimpleNode(f, 16, 3, cfg.Successor(f)), cfg[f]);

        Assert.Equal([], cfg.Predecessor(a));
        Assert.Equal([a], cfg.Predecessor(b));
        Assert.Equal([b], cfg.Predecessor(c));
        Assert.Equal([a], cfg.Predecessor(d));
        Assert.Equal([b, d], cfg.Predecessor(e));
        Assert.Equal([c, d, e], cfg.Predecessor(f));

        Assert.Equal([a, b, c, d, e, f], cfg.Labels());
    }

    [Fact]
    public void ConditionalBranchRequiresFallthroughInstruction()
    {
        var builder = new InstructionBlockPartitioner(1, Label.Create);

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddBrIf(0, 0));
    }

    [Fact]
    public void BranchTargetMustReferenceInstruction()
    {
        var builder = new InstructionBlockPartitioner(2, Label.Create);

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddBr(0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddBr(0, -1));
    }

    [Fact]
    public void ReachableGapsPreserveRangesAndBindAllLabelsBeforeCreatingPayloads()
    {
        var partitioner = new InstructionBlockPartitioner(6, Label.FromIndex);
        var target = partitioner.AddBr(0, 4);
        partitioner.AddReturn(5);

        var blocks = partitioner.BuildReachable(
            new HashSet<int> { 0, 4, 5 },
            (label, range, successor) =>
            {
                Assert.Same(target, partitioner[4]);
                Assert.Same(partitioner.Entry, partitioner[0]);
                return CreateNode(label, range, successor);
            },
            static block => block.Successor,
            PrintBlocks);

        Assert.Same(partitioner.Entry, blocks.EntryLabel);
        Assert.Equal([(0, 1), (4, 2)], blocks.Blocks.Select(block => (block.Start, block.Count)));
        Assert.Same(target, blocks.Blocks[1].Label);
        Assert.Same(target, Assert.IsType<UnconditionalSuccessor>(blocks.Blocks[0].Successor).Target);
        Assert.IsType<TerminateSuccessor>(blocks.Blocks[1].Successor);
    }

    [Fact]
    public void ReachableGapsCannotBeSkippedByOrdinaryFallthrough()
    {
        var partitioner = new InstructionBlockPartitioner(3, Label.FromIndex);
        var exception = Assert.Throws<InvalidOperationException>(() => partitioner.BuildReachable(
            new HashSet<int> { 0, 2 }, CreateNode, static block => block.Successor, PrintBlocks));

        Assert.Contains("falls through to unreachable instruction 1", exception.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryExplicitArmMustTargetAReachableInstruction(bool conditional)
    {
        var partitioner = new InstructionBlockPartitioner(3, Label.FromIndex);
        if (conditional)
            partitioner.AddBrIf(0, 2);
        else
            partitioner.AddBr(0, 2);

        Assert.Throws<InvalidOperationException>(() => partitioner.BuildReachable(
            new HashSet<int> { 0, 1 }, CreateNode, static block => block.Successor, PrintBlocks));
        if (conditional)
            Assert.Throws<InvalidOperationException>(() => partitioner.BuildReachable(
                new HashSet<int> { 0, 2 }, CreateNode, static block => block.Successor, PrintBlocks));
    }

    [Fact]
    public void ReachableIndicesRequireEntryAndOriginalBounds()
    {
        var partitioner = new InstructionBlockPartitioner(3, Label.FromIndex);
        Assert.Throws<ArgumentException>(() => partitioner.BuildReachable(
            new HashSet<int> { 1, 2 }, CreateNode, static block => block.Successor, PrintBlocks));
        Assert.Throws<ArgumentOutOfRangeException>(() => partitioner.BuildReachable(
            new HashSet<int> { -1, 0 }, CreateNode, static block => block.Successor, PrintBlocks));
        Assert.Throws<ArgumentOutOfRangeException>(() => partitioner.BuildReachable(
            new HashSet<int> { 0, 3 }, CreateNode, static block => block.Successor, PrintBlocks));
    }

    [Fact]
    public void FinalReturnAndBranchAreValidAndPublishedBlocksDoNotChange()
    {
        var partitioner = new InstructionBlockPartitioner(1, Label.FromIndex);
        var before = partitioner.Build(CreateNode, static block => block.Successor, PrintBlocks);
        partitioner.AddBr(0, 0);
        var loop = partitioner.Build(CreateNode, static block => block.Successor, PrintBlocks);

        Assert.IsType<TerminateSuccessor>(Assert.Single(before.Blocks).Successor);
        Assert.Same(loop.EntryLabel,
            Assert.IsType<UnconditionalSuccessor>(Assert.Single(loop.Blocks).Successor).Target);

        var returning = new InstructionBlockPartitioner(1, Label.FromIndex);
        returning.AddReturn(0);
        var returned = returning.Build(CreateNode, static block => block.Successor, PrintBlocks);
        Assert.IsType<TerminateSuccessor>(Assert.Single(returned.Blocks).Successor);
    }

    [Fact]
    public void PayloadFactoryCannotReplaceBoundLabels()
    {
        var partitioner = new InstructionBlockPartitioner(1, Label.FromIndex);
        Assert.Throws<ArgumentException>(() => partitioner.Build(
            (label, range, successor) => new SimpleNode(Label.FromIndex(0), range.Start, range.Count, successor),
            static block => block.Successor,
            PrintBlocks));
    }
}
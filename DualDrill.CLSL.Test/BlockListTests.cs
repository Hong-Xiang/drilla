using System.CodeDom.Compiler;
using System.Collections.Immutable;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Test;

public sealed class BlockListTests
{
    private sealed record Block(Label Label, ISuccessor Control) : ILabeledEntity;

    [Fact]
    public void DefinitionsMustBeInitializedNonemptyAndUniquelyLabelled()
    {
        var label = Label.Create("entry");
        var block = new Block(label, Successor.Terminate());

        Assert.Throws<ArgumentException>(() => Create(label, default));
        Assert.Throws<ArgumentException>(() => Create(label, []));
        Assert.Throws<ArgumentException>(() => Create(label, [block, block]));
        Assert.Throws<ArgumentException>(() => Create(label, [block, new Block(label, Successor.Terminate())]));
    }

    [Fact]
    public void EntryAndEveryControlTargetMustBeDefinedByIdentity()
    {
        var label = Label.Create("same-name");
        var missing = Label.Create("same-name");
        var block = new Block(label, Successor.Terminate());

        Assert.Throws<ArgumentException>(() => Create(missing, [block]));
        Assert.Throws<ArgumentException>(() => Create(label, [new Block(label, Successor.Unconditional(missing))]));
        Assert.Throws<ArgumentException>(() => Create(label, [new Block(label, Successor.Conditional(missing, label))]));
        Assert.Throws<ArgumentException>(() => Create(label, [new Block(label, Successor.Conditional(label, missing))]));
    }

    [Fact]
    public void GraphFactoryPreservesStorageDefinitionsNonFirstEntryAndPayloadIdentity()
    {
        var first = new Block(Label.Create("same-name"), Successor.Terminate());
        var last = new Block(Label.Create("same-name"), Successor.Terminate());
        var entry = new Block(Label.Create("same-name"), Successor.Conditional(last.Label, first.Label));
        var disconnected = new Block(Label.Create("same-name"), Successor.Terminate());
        ImmutableArray<Block> definitions = [first, entry, last, disconnected];
        var blocks = Create(entry.Label, definitions);

        var graph = ControlFlowGraph.Create(blocks, static block => block.Control);

        Assert.Equal(definitions, blocks.Blocks);
        Assert.Same(entry.Label, blocks.EntryLabel);
        Assert.Same(entry.Label, graph.EntryLabel);
        Assert.Equal(4, graph.Count);
        Assert.Equal([entry.Label, first.Label, last.Label], graph.Labels());
        Assert.Empty(graph.Predecessor(disconnected.Label));
        foreach (var block in definitions)
        {
            Assert.Same(block, graph[block.Label]);
            Assert.Same(block.Label, graph[block.Label].Label);
            Assert.Same(block.Control, graph.Successor(block.Label));
        }

        Assert.Equal([last.Label, first.Label], graph.GetSucc(entry.Label));
    }

    [Fact]
    public void GraphFactoryPreservesParallelArmsWithoutDuplicatingPredecessorNodes()
    {
        var label = Label.Create();
        var block = new Block(label, Successor.Conditional(label, label));
        var graph = ControlFlowGraph.Create(Create(label, [block]), static item => item.Control);

        Assert.Equal([label, label], graph.GetSucc(label));
        Assert.Same(block.Control, graph.Successor(label));
        Assert.Same(label, Assert.Single(graph.Predecessor(label)));
    }

    private static BlockList<Block> Create(Label entry, ImmutableArray<Block> blocks) =>
        new(entry, blocks, static block => block.Control, PrintBlocks);

    private static void PrintBlocks(
        BlockList<Block> blocks,
        IndentedTextWriter writer,
        PrettyPrintOption option)
    {
        foreach (var block in blocks.Blocks)
            writer.WriteLine(block);
    }
}

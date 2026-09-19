using System.CodeDom.Compiler;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language.ControlFlow;

/// <summary>
/// Ordered immutable block definitions, without graph indexes or reachability requirements.
/// Payloads must be immutable; control projections must be pure views of those payloads.
/// </summary>
public sealed class BlockList<TBlock> : IPrintable where TBlock : ILabeledEntity
{
    private readonly Action<BlockList<TBlock>, IndentedTextWriter, PrettyPrintOption> prettyPrint;

    public BlockList(
        Label entry,
        ImmutableArray<TBlock> blocks,
        Func<TBlock, ISuccessor> getSuccessor,
        Action<BlockList<TBlock>, IndentedTextWriter, PrettyPrintOption> prettyPrint)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(getSuccessor);
        ArgumentNullException.ThrowIfNull(prettyPrint);
        if (blocks.IsDefaultOrEmpty)
            throw new ArgumentException("A block list must contain at least one block.", nameof(blocks));

        var labels = new HashSet<Label>(ReferenceEqualityComparer.Instance);
        foreach (var block in blocks)
        {
            if (block is null || block.Label is null)
                throw new ArgumentException("Every block must have a label.", nameof(blocks));
            if (!labels.Add(block.Label))
                throw new ArgumentException("Block labels must be unique.", nameof(blocks));
        }

        if (!labels.Contains(entry))
            throw new ArgumentException("Entry label not found in block definitions.", nameof(entry));

        foreach (var block in blocks)
            foreach (var target in getSuccessor(block).AllTargets())
                if (!labels.Contains(target))
                    throw new ArgumentException(
                        $"Control target {target} not found in block definitions.", nameof(blocks));

        EntryLabel = entry;
        Blocks = blocks;
        this.prettyPrint = prettyPrint;
    }

    public Label EntryLabel { get; }
    public ImmutableArray<TBlock> Blocks { get; }

    public void PrettyPrint(IndentedTextWriter writer, PrettyPrintOption option) =>
        prettyPrint(this, writer, option);
}

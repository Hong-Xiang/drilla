using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Symbol;
using System.CodeDom.Compiler;
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.FunctionBody;
public sealed class StackIRFunctionBody3
    : IUnifiedFunctionBody<StackIRBasicBlock>
    , ILocalDeclarationContext
{
    private readonly FrozenDictionary<Label, StackIRBasicBlock> Blocks;

    public StackIRFunctionBody3(
        Label entry,
        FrozenDictionary<Label, StackIRBasicBlock> blocks)
    {
        Entry = entry;
        Blocks = blocks;
        Labels = GetLabels();
        LabelIndeices = Labels.Select((l, i) => (l, i)).ToFrozenDictionary(x => x.l, x => x.i);
    }

    ImmutableArray<Label> GetLabels()
    {
        HashSet<Label> visited = [];
        Queue<Label> queue = [];
        List<Label> labels = [];
        queue.Enqueue(Entry);
        while (queue.Count > 0)
        {
            var l = queue.Dequeue();
            if (visited.Contains(l))
            {
                continue;
            }
            labels.Add(l);
            visited.Add(l);
            foreach (var sl in Blocks[l].Successor.AllTargets())
            {
                queue.Enqueue(sl);
            }
        }
        return [.. labels];
    }

    public StackIRBasicBlock this[Label label] => Blocks[label];

    public Label Entry { get; }

    public ILocalDeclarationContext DeclarationContext => this;

    public ImmutableArray<VariableDeclaration> LocalVariables => [];


    public ImmutableArray<Label> Labels { get; }
    FrozenDictionary<Label, int> LabelIndeices { get; }

    public ImmutableArray<IShaderValue> Values => [];

    public IUnifiedFunctionBody<TResultBasicBlock> ApplyTransform<TResultBasicBlock>(IBasicBlockTransform<StackIRBasicBlock, TResultBasicBlock> transform)
        where TResultBasicBlock : IBasicBlock2
        => throw new NotImplementedException();

    public void Dump(IndentedTextWriter writer)
    {
        writer.Write($"entry ");
        Entry.Dump(this, writer);
        writer.WriteLine($" in {Blocks.Count} blocks");
        writer.WriteLine();
        foreach (var block in Blocks)
        {
            block.Value.Dump(this, writer);
            writer.WriteLine();
        }
    }

    public int LabelIndex(Label label)
        => LabelIndeices[label];
    public ISuccessor Successor(Label label)
        => Blocks[label].Successor;

    public TResult Traverse<TElementResult, TResult>(IControlFlowElementSequenceTraverser<StackIRBasicBlock, TElementResult, TResult> traverser)
    {
        throw new NotImplementedException();
    }

    public int ValueIndex(IShaderValue value)
    {
        throw new NotImplementedException();
    }

    public int VariableIndex(VariableDeclaration variable)
    {
        throw new NotImplementedException();
    }
}

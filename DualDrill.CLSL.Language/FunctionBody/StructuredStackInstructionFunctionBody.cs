using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using System.CodeDom.Compiler;
using System.Collections.Frozen;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Value;

namespace DualDrill.CLSL.Language.FunctionBody;

// TODO: refactor to simply FunctionBody<IStructuredControlFlowRegion<IStructuredStackInstruction>>
public sealed class StructuredStackInstructionFunctionBody : IFunctionBody, ILocalDeclarationContext
{
    public IStructuredControlFlowRegion Root { get; }

    public StructuredStackInstructionFunctionBody(IStructuredControlFlowRegion root)
    {
        Root = root;
        Labels = [..Root.ReferencedLabels.Distinct()];
        LocalVariables = [..Root.ReferencedLocalVariables.Distinct()];
        VariableIndices = LocalVariables.Index().ToFrozenDictionary(x => x.Item, x => x.Index);
        LabelIndices = Labels.Index().ToFrozenDictionary(x => x.Item, x => x.Index);
    }

    public IEnumerable<VariableDeclaration> FunctionBodyDataLocalVariables => Root.ReferencedLocalVariables;

    public IEnumerable<Label> FunctionBodyDataLabels => Root.ReferencedLabels;
    FrozenDictionary<VariableDeclaration, int> VariableIndices { get; }
    FrozenDictionary<Label, int> LabelIndices { get; }

    public int ValueIndex(IValue value)
    {
        throw new NotImplementedException();
    }

    public int VariableIndex(VariableDeclaration variable)
        => VariableIndices[variable];

    public int LabelIndex(Label label)
        => LabelIndices[label];


    public ImmutableArray<VariableDeclaration> LocalVariables { get; }
    public ImmutableArray<Label> Labels { get; }
    public ImmutableArray<IValue> Values { get; }

    public void Dump(IndentedTextWriter writer)
    {
        Root.Dump(this, writer);
    }

    public ILocalDeclarationContext DeclarationContext => this;
}
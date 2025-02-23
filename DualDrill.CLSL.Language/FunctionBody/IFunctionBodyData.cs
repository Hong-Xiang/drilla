using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.Common.CodeTextWriter;
using System.CodeDom.Compiler;
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.FunctionBody;

public interface IFunctionBody : ILocalDeclarationContext, ITextDumpable
{
}

public sealed class FunctionBody<TBodyData>
    : IFunctionBody
    , ITextDumpable
    where TBodyData : ILocalDeclarationReferencingElement
{
    public TBodyData Body { get; }
    FrozenDictionary<VariableDeclaration, int> VariableIndices { get; }
    FrozenDictionary<Label, int> LabelIndices { get; }

    public ImmutableArray<VariableDeclaration> LocalVariables { get; }
    public ImmutableArray<Label> Labels { get; }

    public FunctionBody(TBodyData body)
    {
        Body = body;
        LocalVariables = [..body.ReferencedLocalVariables.Distinct()];
        Labels = [..body.ReferencedLabels.Distinct()];
        VariableIndices = LocalVariables.Index().ToFrozenDictionary(x => x.Item, x => x.Index);
        LabelIndices = Labels.Index().ToFrozenDictionary(x => x.Item, x => x.Index);
    }

    public int VariableIndex(VariableDeclaration variable)
        => VariableIndices[variable];

    public int LabelIndex(Label label)
        => LabelIndices[label];


    public void Dump(IndentedTextWriter writer)
    {
        Body.Dump(this, writer);
    }
}
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.Common.CodeTextWriter;
using System.CodeDom.Compiler;
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.FunctionBody;

/// <summary>
/// Currently support 3 kinds of function body representation:
/// * UnstructedControlFlowInstructionFunctionBody
/// * StructuredStackInstructionFunctionBody
/// * CompondStatement
/// </summary>
public interface IFunctionBodyData
    : ITextDumpable<ILocalDeclarationContext>
{
    IEnumerable<VariableDeclaration> FunctionBodyDataLocalVariables { get; }
    IEnumerable<Label> FunctionBodyDataLabels { get; }
}

public interface IFunctionBody : ILocalDeclarationContext
{
}

public sealed class FunctionBody<TBodyData>
    : IFunctionBody
    , ITextDumpable
    where TBodyData : IFunctionBodyData
{
    public TBodyData Body { get; }
    FrozenDictionary<VariableDeclaration, int> VariableIndices { get; }
    FrozenDictionary<Label, int> LabelIndices { get; }

    public ImmutableArray<VariableDeclaration> LocalVariables { get; }
    public ImmutableArray<Label> Labels { get; }

    public FunctionBody(TBodyData body)
    {
        Body = body;
        LocalVariables = [..body.FunctionBodyDataLocalVariables.Distinct()];
        Labels = [..body.FunctionBodyDataLabels.Distinct()];
        VariableIndices = LocalVariables.Index().ToFrozenDictionary(x => x.Item, x => x.Index);
        LabelIndices = Labels.Index().ToFrozenDictionary(x => x.Item, x => x.Index);
    }

    public int VariableIndex(VariableDeclaration variable)
        => VariableIndices[variable];

    public int LabelIndex(Label label)
        => LabelIndices[label];

    public string VariableName(VariableDeclaration variable)
    {
        return variable.DeclarationScope == DeclarationScope.Function
            ? $"var%{VariableIndex(variable)} {variable}"
            : $"module var {variable.Name}";
    }

    public string LabelName(Label label) => $"label%{LabelIndex(label)} {label}";

    public void Dump(IndentedTextWriter writer)
    {
        Body.Dump(this, writer);
    }
}
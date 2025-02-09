using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.Common.CodeTextWriter;
using System.CodeDom.Compiler;
using System.Collections.Frozen;

namespace DualDrill.CLSL.Language.FunctionBody;

/// <summary>
/// Currently support 3 kinds of function body representation:
/// * UnstructedControlFlowInstructionFunctionBody
/// * StructuredStackInstructionFunctionBody
/// * CompondStatement
/// </summary>
public interface IFunctionBodyData
    : ITextDumpable<IFunctionBody>
{
    IEnumerable<VariableDeclaration> LocalVariables { get; }
    IEnumerable<Label> Labels { get; }
}

public interface IFunctionBody
{
    public int VariableIndex(VariableDeclaration variable);
    public int LabelIndex(Label label);
    public IReadOnlySet<VariableDeclaration> LocalVariables { get; }
    public IReadOnlySet<Label> Labels { get; }
}

public sealed class FunctionBody<TBody>
    : IFunctionBody
    , ITextDumpable
    where TBody : IFunctionBodyData
{
    public TBody Body { get; }
    FrozenDictionary<VariableDeclaration, int> VariableIndices { get; }
    FrozenDictionary<Label, int> LabelIndices { get; }

    public IReadOnlySet<VariableDeclaration> LocalVariables { get; }
    public IReadOnlySet<Label> Labels { get; }

    public FunctionBody(TBody body)
    {
        Body = body;
        LocalVariables = body.LocalVariables.Distinct().ToHashSet();
        Labels = body.Labels.Distinct().ToHashSet();
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

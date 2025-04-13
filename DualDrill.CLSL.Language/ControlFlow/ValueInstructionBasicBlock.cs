using System.CodeDom.Compiler;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Language.Value;
using DualDrill.CLSL.Language.ValueInstruction;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.ControlFlow;

public sealed class ValueInstructionBasicBlock : IBasicBlock2<IValueInstruction, IBlockArgumentValue, IValue>
{
    public ValueInstructionBasicBlock(Label label,
        ImmutableArray<IValueInstruction> elements,
        ImmutableArray<IBlockArgumentValue> inputs,
        ImmutableArray<IValue> outputs)
    {
        if (elements.Length == 0)
        {
            throw new ArgumentException("Stack instruction must have at least one element", nameof(elements));
        }

        if (elements[^1] is not ITerminatorValueInstruction terminator)
        {
            throw new ArgumentException("Last stack instruction must implement IStackTerminatorInstruction",
                nameof(elements));
        }


        Label = label;
        Elements = elements;
        Inputs = inputs;
        Outputs = outputs;
        Terminator = terminator;
        Successor = terminator.ToSuccessor();
    }

    public ITerminatorValueInstruction Terminator { get; }

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.Write($"block {context.LabelName(Label)}: [");
        writer.Write(string.Join(", ", Inputs.Select(t => t.Type.Name)));
        writer.Write("] -> [");
        writer.Write(string.Join(", ", Outputs.Select(t => t.Type.Name)));
        writer.WriteLine("]");

        using (writer.IndentedScope())
        {
            foreach (var e in Elements)
            {
                e.Dump(context, writer);
            }
        }
    }

    public Label Label { get; }
    public ISuccessor Successor { get; }
    public ImmutableArray<IValueInstruction> Elements { get; }
    public ImmutableArray<IBlockArgumentValue> Inputs { get; }
    public ImmutableArray<IValue> Outputs { get; }

    public IEnumerable<IOperationValue> OperationValues =>
        Elements.OfType<IExpressionValueInstruction>().Select(x => x.ResultValue);

    public IEnumerable<VariableDeclaration> ReferencedLocalVariables =>
        Elements.SelectMany(e => e.ReferencedLocalVariables);

    public IEnumerable<IValue> ReferencedValues => [..Inputs, ..OperationValues];
}
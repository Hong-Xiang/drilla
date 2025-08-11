using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.ControlFlow;

public sealed class ControlFlowGraphNodeBasicBlock : IBasicBlock2
{
    public ControlFlowGraphNodeBasicBlock(Label label, ISuccessor successor)
    {
        Label = label;
        Successor = successor;
    }

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.Write($"block %{context.LabelName(Label)}: [");
        writer.WriteLine(context.LabelName(Label));
        using (writer.IndentedScope())
        {
            writer.WriteLine("successor: ");
            using (writer.IndentedScope())
            {
                Successor.Dump(context, writer);
            }
        }

        writer.WriteLine();
    }

    public Label Label { get; }
    public ISuccessor Successor { get; }
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => [];
    public IEnumerable<IShaderValue> ReferencedValues => [];
}
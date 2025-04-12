using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.LinearInstruction;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.ControlFlow;

public interface IBasicBlock2<TElement, TInput, TOutput>
    : ITextDumpable<ILocalDeclarationContext>
    where TElement : ILocalDeclarationReferencingElement
{
    Label Label { get; }
    ImmutableArray<TElement> Elements { get; }
    ImmutableArray<TInput> Inputs { get; }
    ImmutableArray<TOutput> Outputs { get; }
    ISuccessor Successor { get; }
}

public sealed record class StackInstructionBasicBlock(
    Label Label,
    ImmutableArray<IInstruction> Elements,
    ImmutableArray<IShaderType> Inputs,
    ImmutableArray<IShaderType> Outputs,
    ISuccessor Successor)
    : IBasicBlock2<IInstruction, IShaderType, IShaderType>
{
    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.Write($"block %{context.LabelName(Label)}: [");
        writer.Write(string.Join(", ", Inputs.Select(t => t.Name)));
        writer.Write("] -> [");
        writer.Write(string.Join(", ", Outputs.Select(t => t.Name)));
        writer.WriteLine("]");

        using (writer.IndentedScope())
        {
            foreach (var e in Elements)
            {
                e.Dump(context, writer);
            }
        }
    }
}

public interface IBasicBlock<TElement, TResult, TTransfer>
    : ITextDumpable<ILocalDeclarationContext>
{
    public ImmutableArray<TElement> Elements { get; }
    public ImmutableStack<TTransfer> Inputs { get; }
    public ImmutableStack<TTransfer> Outputs { get; }
}

public sealed record class BasicBlock<TElement>(
    ImmutableArray<TElement> Elements,
    ImmutableStack<VariableDeclaration> Inputs,
    ImmutableStack<VariableDeclaration> Outputs
)
    : IBasicBlock<TElement, IExpression, VariableDeclaration>
    where TElement : ILocalDeclarationReferencingElement
{
    public static BasicBlock<TElement> Create(IEnumerable<TElement> instructions)
    {
        return new([.. instructions], [], []);
    }

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        foreach (var e in Elements)
        {
            e.Dump(context, writer);
        }
    }
}
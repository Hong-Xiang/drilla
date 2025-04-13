using System.CodeDom.Compiler;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.ControlFlow;

// for stack instruction, BasicBlock<IStackInstruction, IShaderType, IShaderType>
// for value instruction, BasicBlock<IValueInstruction, ??? (maybe IValue, or dedicate BlockArgumentValue?), IValue>
// for statement, BasicBlock<IStatement, Unit, Unit>, implies empty Inputs and Outputs

public interface IBasicBlock2
    : ITextDumpable<ILocalDeclarationContext>
    , ILocalDeclarationReferencingElement
{
    Label Label { get; }
    ISuccessor Successor { get; }

    IEnumerable<Label> ILocalDeclarationReferencingElement.ReferencedLabels =>
        [Label, ..Successor.GetReferencedLabels()];
}

public interface IBasicBlock2<TElement, TInput, TOutput>
    : IBasicBlock2
    where TElement : ILocalDeclarationReferencingElement
{
    ImmutableArray<TElement> Elements { get; }
    ImmutableArray<TInput> Inputs { get; }
    ImmutableArray<TOutput> Outputs { get; }
}

public sealed record class BasicBlock<TElement>(
    ImmutableArray<TElement> Elements,
    ImmutableStack<VariableDeclaration> Inputs,
    ImmutableStack<VariableDeclaration> Outputs
)
    : ITextDumpable<ILocalDeclarationContext>
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
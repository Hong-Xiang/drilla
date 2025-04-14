using System.Collections.Immutable;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.FunctionBody;

public interface IBasicBlock2
    : ITextDumpable<ILocalDeclarationContext>
    , ILocalDeclarationReferencingElement
{
    Label Label { get; }
    ISuccessor Successor { get; }

    IEnumerable<Label> ILocalDeclarationReferencingElement.ReferencedLabels =>
        [Label, ..Successor.GetReferencedLabels()];
}

// for stack instruction, BasicBlock<IStackInstruction, IShaderType, IShaderType>
// for value instruction, BasicBlock<IValueInstruction, ??? (maybe IValue, or dedicate BlockArgumentValue?), IValue>
// for statement, BasicBlock<IStatement, Unit, Unit>, implies empty Inputs and Outputs
public interface IBasicBlock2<TElement, TInput, TOutput>
    : IBasicBlock2
    where TElement : ILocalDeclarationReferencingElement
{
    ImmutableArray<TElement> Elements { get; }
    ImmutableArray<TInput> Inputs { get; }
    ImmutableArray<TOutput> Outputs { get; }
}

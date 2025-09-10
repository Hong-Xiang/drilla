using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Symbol;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.FunctionBody;

[Obsolete]
public interface IBasicBlock2 : IStructuredControlFlowElement
{
    Label Label { get; }
    ISuccessor Successor { get; }

    IEnumerable<Label> IDeclarationUser.ReferencedLabels =>
        [Label, ..Successor.GetReferencedLabels()];
}

[Obsolete]
// for stack instruction, BasicBlock<IStackInstruction, IShaderType, IShaderType>
// for value instruction, BasicBlock<IValueInstruction, ??? (maybe IValue, or dedicate BlockArgumentValue?), IValue>
// for statement, BasicBlock<IStatement, Unit, Unit>, implies empty Inputs and Outputs
public interface IBasicBlock2<TElement, TInput, TOutput> : IBasicBlock2
    where TElement : IDeclarationUser
{
    ImmutableArray<TElement> Elements { get; }
    ImmutableArray<TInput> Inputs { get; }
    ImmutableArray<TOutput> Outputs { get; }
}

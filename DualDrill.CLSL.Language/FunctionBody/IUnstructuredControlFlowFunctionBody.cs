using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.FunctionBody;

/// <summary>
/// Control flow graph representation of function body,
/// LabelIndex(label) is reverse postorder visiting index
/// Predecessor, Dominators, DominatorTreeChildren return label ordered by LabelIndex asc
/// LabelIndex(Entry) = 0
/// </summary>
/// <typeparam name="TElement"></typeparam>
public interface IUnstructuredControlFlowFunctionBody<TElement>
    : IFunctionBody, ITextDumpable
    where TElement : IUnstructuredControlFlowElement
{
    Label Entry { get; }
    BasicBlock<TElement> this[Label label] { get; }
    ISuccessor Successor(Label label);
    IEnumerable<Label> Predecessor(Label label);
    IEnumerable<Label> Dominators(Label label);
    Label? ImmediateDominator(Label label);
    IEnumerable<Label> DominatorTreeChildren(Label label);

    IUnstructuredControlFlowFunctionBody<TResultElement>
        MapBody<TResultElement>(Func<BasicBlock<TElement>, BasicBlock<TResultElement>> f)
        where TResultElement : IUnstructuredControlFlowElement;
}
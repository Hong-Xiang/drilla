namespace DualDrill.CLSL.Language.ControlFlow;

public interface IUnstructuredControlFlowElement
{
}

public interface IUnstructuredControlFlowElement<TSelf>
    : IUnstructuredControlFlowElement
    where TSelf : IUnstructuredControlFlowElement
{
}
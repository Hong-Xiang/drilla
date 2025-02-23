using DualDrill.CLSL.Language.Declaration;

namespace DualDrill.CLSL.Language.ControlFlow;

public interface IBasicBlockElement : IStructuredControlFlowElement
{
}

public interface IBasicBlockElement<TSelf>
    : IBasicBlockElement
    where TSelf : IBasicBlockElement
{
}
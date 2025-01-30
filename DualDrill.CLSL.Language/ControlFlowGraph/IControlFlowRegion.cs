namespace DualDrill.CLSL.Language.ControlFlowGraph;

/// <summary>
/// The main external interface of CLSL IR collection,
/// Basically encoding of Loop | Block
/// </summary>
public interface IControlFlowRegion : IControlFlowRegionElement
{
    public IEnumerable<IControlFlowRegionElement> Children { get; }
}

/// <summary>
/// Encoding of Loop | Block | BasicBlock
/// </summary>
public interface IControlFlowRegionElement
{
}


namespace DualDrill.CLSL.Language.ControlFlowGraph;

public sealed class Loop : IControlFlowRegion
{
    public IEnumerable<IControlFlowRegionElement> Children => throw new NotImplementedException();
}

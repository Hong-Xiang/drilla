using System.Collections.Immutable;

namespace DualDrill.CLSL.Frontend;

public sealed class PreStack
{
    internal PreStack(ImmutableStack<CilStackType> types)
    {
        ArgumentNullException.ThrowIfNull(types);
        Types = types;
    }

    public ImmutableStack<CilStackType> Types { get; }
}

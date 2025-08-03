namespace DualDrill.CLSL.Language.Symbol;

public interface IShaderSymbol
{
    //string? Name { get; }
}


public interface IShaderSymbol<out TResult> : IShaderSymbol
{
    TResult? Lookup(ISymbolTableView table);
}

public sealed record class SimpleNamedSymbol<TTarget>(string Name, TTarget Value) : IShaderSymbol<TTarget>
{
    public TTarget? Lookup(ISymbolTableView table)
        => Value;
}

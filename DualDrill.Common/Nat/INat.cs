namespace DualDrill.Common.Nat;

/// <summary>
/// Used as type level encoding of commonly used natural numbers.
/// Use INat.Value/Nat.FromValue for C# int interop
/// Use INat.Accept(visitor) to map INat value to concrete type (implicitly using CPS style)
/// Use N??.Instance to map type to INat value
/// </summary>
public interface INat
{
    static abstract int Value { get; }

    public T Accept<T>(INatVisitor<T> visitor);
}

public interface INat<TSelf> : INat, ISingleton<TSelf>
    where TSelf : INat<TSelf>
{
}

public interface INatVisitor<T>
{
    public T Visit<TNat>() where TNat : INat;
}


using DualDrill.Common;

namespace DualDrill.CLSL.Language.Types;

public interface ISignedness<TSign>
    where TSign : ISignedness<TSign>
{
    abstract static string Name { get; }
}

public static class Signedness
{
    public readonly struct S : ISignedness<S>
    {
        public static string Name => "s";
    }

    public readonly struct U : ISignedness<U>
    {
        public static string Name => "u";
    }
}

public interface IProtocol
{
    interface IPattern2<TResult>
    {
        TResult P1(P1 value);
        TResult P2(P2 value);
    }

    TResult Match2<TPattern, TResult>(TPattern pattern) where TPattern : IPattern2<TResult>;
    interface IPattern<TResult>
    {
        TResult Case<TProtocol>() where TProtocol : IProtocol<TProtocol>;
    }
}


public interface IProtocol<TSelf> : IProtocol, ISingleton<TSelf>
    where TSelf : IProtocol<TSelf>
{
    public TResult MatchG<TResult>(IPattern<TResult> pattern) => pattern.Case<TSelf>();
}

public readonly struct P1 : IProtocol<P1>
{
    public static P1 Instance => new();

    public TResult Match2<TPattern, TResult>(TPattern pattern) where TPattern : IProtocol.IPattern2<TResult>
        => pattern.P1(this);
}

public readonly struct P2 : IProtocol<P2>
{
    public static P2 Instance => new();

    public TResult Match2<TPattern, TResult>(TPattern pattern) where TPattern : IProtocol.IPattern2<TResult>
        => pattern.P2(this);
}

static class Protocol
{
    public static void Foo<T1, T2>()
        where T1 : IProtocol<T1>
        where T2 : IProtocol<T2>
    {
        var result = (T1.Instance, T2.Instance) switch
        {
            (P1, P1) => 0,
            (P1, P2) => 1,
            (P2, P1) => 2,
            (P2, P2) => 3,
            _ => throw new Exception()
        };
    }
}

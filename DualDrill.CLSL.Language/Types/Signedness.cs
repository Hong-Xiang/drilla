namespace DualDrill.CLSL.Language.Types;

public interface ISignedness
{
}

public interface ISignedness<TSign> : ISignedness
    where TSign : ISignedness<TSign>
{
    static abstract string Name { get; }
}

public static class Signedness
{
    public readonly struct S : ISignedness<S>
    {
        public static S Instance => new();
        public static string Name => "s";
    }

    public readonly struct U : ISignedness<U>
    {
        public static U Instance => new();
        public static string Name => "u";
    }
}
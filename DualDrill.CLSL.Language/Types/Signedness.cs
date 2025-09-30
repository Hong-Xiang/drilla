namespace DualDrill.CLSL.Language.Types;

public interface ISignedness<TSign>
    where TSign : ISignedness<TSign>
{
    static abstract string Name { get; }
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
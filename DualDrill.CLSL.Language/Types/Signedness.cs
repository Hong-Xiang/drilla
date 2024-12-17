namespace DualDrill.CLSL.Language.Types;

public interface ISignedness
{
    string Name { get; }
}

public static class Signedness
{
    public struct S : ISignedness
    {
        public readonly string Name => "s";
    }
    public struct U : ISignedness
    {
        public readonly string Name => "u";
    }
}

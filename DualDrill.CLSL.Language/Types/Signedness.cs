using DotNext.Patterns;

namespace DualDrill.CLSL.Language.Types;

public interface ISignedness
{
    string Name { get; }
}

public interface ISignedness<TSign>
    : ISignedness, ISingleton<TSign>
    where TSign : class, ISignedness<TSign>
{
}

public static class Signedness
{
    public sealed class S : ISignedness<S>
    {
        public static S Instance { get; } = new();

        public string Name => "s";
    }
    public sealed class U : ISignedness<U>
    {
        public static U Instance { get; } = new();

        public string Name => "u";
    }
}

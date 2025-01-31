namespace DualDrill.CLSL.Language.Operation;

public static class Swizzle
{
    interface IComponent<TSelf>
        where TSelf : IComponent<TSelf>
    {
    }
    public struct X : IComponent<X>
    {
    }
    public struct Y : IComponent<Y>
    {
    }
    public struct Z : IComponent<Z>
    {
    }
    public struct W : IComponent<W>
    {
    }

    public interface IPattern<TSelf>
            where TSelf : IPattern<TSelf>
    {
    }

    public struct Pattern<TX> : IPattern<Pattern<TX>>
    {
    }

    public struct Pattern<TX, TY> : IPattern<Pattern<TX, TY>>
    {
    }

    public struct Pattern<TX, TY, TZ> : IPattern<Pattern<TX, TY, TZ>>
    {
    }

    public struct Pattern<TX, TY, TZ, TW> : IPattern<Pattern<TX, TY, TZ, TW>>
    {
    }
}

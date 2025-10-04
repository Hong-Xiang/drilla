using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Common;

public sealed class Higher
{
}

interface IKind1<K>
{
}

public interface IHigher<K, T>
{
}

public interface IHigher<TSelf, K, T> : IHigher<K, T>
    where TSelf : IHigher<TSelf, K, T>
{
}

sealed class ArrayTag : IFunctor<ArrayTag>
{
    public static IHigher<ArrayTag, T> Inj<T>(T[] value) => new Carrier<T>(value);

    public static IHigher<ArrayTag, B> Map<A, B>(IHigher<ArrayTag, A> source, Func<A, B> f)
    {
        return Inj([.. Prj(source).Select(f)]);
    }

    public static T[] Prj<T>(IHigher<ArrayTag, T> value) => ((Carrier<T>)value).Value;

    sealed record class Carrier<T>(T[] Value) : IHigher<Carrier<T>, ArrayTag, T>
    {
    }
}



sealed class NullableTag { }

interface IFunctor<K>
{
    abstract static IHigher<K, B> Map<A, B>(IHigher<K, A> source, Func<A, B> f);
}

static class Test
{
    public static IHigher<K, int> TestAll<K>(IHigher<K, int> source)
            where K : IFunctor<K>
    {
        return K.Map(source, a => a + 1);
    }

    public static void Foo()
    {
        TestAll(ArrayTag.Inj([1, 2, 3]));
    }
}

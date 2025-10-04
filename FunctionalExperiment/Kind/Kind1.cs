namespace FunctionalExperiment.Kind;


public interface IKind1<TSelf>
    where TSelf : IKind1<TSelf>
{
    static abstract TSelf Instance { get; }
}



public interface IFunctor<TSelf>
    : IKind1<TSelf>
    where TSelf : IFunctor<TSelf>
{
    IAlgebra1<TSelf, TS, IK<TSelf, TR>> FunctorMap<TS, TR>(Func<TS, TR> f);
}

public interface IAlgebra1<out TKind, in TI, out TO>
{
    IAlgebra1<TKind, TS, TR> DiMap<TS, TR>(Func<TS, TI> f, Func<TO, TR> g);
}

public interface INaturalTransform<TF, TG>
    where TF : IFunctor<TF>
    where TG : IFunctor<TG>
{
    IK<TG, T> Apply<T>(IK<TF, T> value);
}

public interface IK<in TKind, out T>
{
    TR Evaluate<TR>(IAlgebra1<TKind, T, TR> algebra);
}

public interface IFree1<TSelf>
    : IFunctor<TSelf>
    where TSelf : IFree1<TSelf>
{
    IAlgebra1<TSelf, T, IK<TSelf, T>> FreeFactory<T>();
    IAlgebra1<TSelf, TS, IK<TSelf, TR>> IFunctor<TSelf>.FunctorMap<TS, TR>(Func<TS, TR> f)
        => FreeFactory<TR>().DiMap(f, static x => x);
}

readonly record struct Term<TF>(IK<TF, Term<TF>> Value)
    where TF : IFunctor<TF>
{
    sealed class Folder<T>(IAlgebra1<TF, T, T> alg)
    {
        public T Fold(Term<TF> expr)
            => expr.Value.Select(Fold).Evaluate(alg);
    }

    public T Fold<T>(IAlgebra1<TF, T, T> algebra)
        => new Folder<T>(algebra).Fold(this);
}

public static class Kind1Extension
{
    public static IK<TF, TB> Select<TF, TA, TB>(this IK<TF, TA> fa, Func<TA, TB> f)
        where TF : IFunctor<TF>
        => fa.Evaluate(TF.Instance.FunctorMap(f));
}


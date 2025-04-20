namespace FunctionalExperiment.RecursiveScheme;

interface IKind2<TSelf>
    where TSelf : IKind2<TSelf>
{
}

interface IAlgebra2<out TKind, in TA, in TB, out TC>
    where TKind : IKind2<TKind>
{
}

interface IKind2<in TKind, out TA, out TB>
    where TKind : IKind2<TKind>
{
    TResult Evaluate<TResult>(IAlgebra2<TKind, TA, TB, TResult> algebra);
}

interface IProfunctor<TKind> : IKind2<TKind>
    where TKind : IProfunctor<TKind>
{
    static abstract IAlgebra2<TKind, TB, TC, IKind2<TKind, TA, TD>> DiMap<TA, TB, TC, TD>(Func<TA, TB> f,
        Func<TC, TD> g);
}

static class FuncK
{
    public sealed class Kind : IProfunctor<Kind>
    {
        public static IAlgebra2<Kind, TB, TC, IKind2<Kind, TA, TD>> DiMap<TA, TB, TC, TD>(Func<TA, TB> f,
            Func<TC, TD> g)
            => new DiMapAlgebra<TA, TB, TC, TD>(f, g);
    }

    public interface IAlgebra<TA, in TB, out TO> : IAlgebra2<Kind, TA, TB, TO>
    {
        TO Func(Func<TA, TB> f);
    }

    public static IAlgebra<TA, TB, TO> Project<TA, TB, TO>(this IAlgebra2<Kind, TA, TB, TO> algebra)
        => (IAlgebra<TA, TB, TO>)algebra;

    sealed class Carieer<TA, TB>(Func<TA, TB> Func) : IKind2<Kind, TA, TB>
    {
        public TResult Evaluate<TResult>(IAlgebra2<Kind, TA, TB, TResult> algebra)
            => algebra.Project().Func(Func);
    }

    sealed class DiMapAlgebra<TA, TB, TC, TD>(
        Func<TA, TB> f,
        Func<TC, TD> g
    ) : IAlgebra<TB, TC, IKind2<Kind, TA, TD>>
    {
        public IKind2<Kind, TA, TD> Func(Func<TB, TC> s)
            => new Carieer<TA, TD>(a => g(s(f(a))));
    }
}
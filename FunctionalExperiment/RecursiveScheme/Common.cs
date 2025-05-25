using System.Diagnostics.Tracing;

namespace FunctionalExperiment.RecursiveScheme;

sealed record class Lift<TSourceAlgebra, TTargetAlgebra, T>(
    IK<TSourceAlgebra, T> Source
) : IK<TTargetAlgebra, T>
    where TSourceAlgebra : IKind1<TSourceAlgebra>
    where TTargetAlgebra : class, TSourceAlgebra, IKind1<TTargetAlgebra>
{
    public TR Evaluate<TR>(IKindAlgebra1<TTargetAlgebra, T, TR> algebra)
        => Source.Evaluate(algebra);
}

interface IStaticFunc<TSelf, in TS, out TR>
    where TSelf : IStaticFunc<TSelf, TS, TR>
{
    static abstract TR Apply(TS value);
}

// (Term a -> Term a) -> Term a -> Term a
// Term f = IK<F, Term<F>>
// (Term a -> Term a) => IK<F, Term<F>> -> IK<F, Term<F>>
//                    => IAlgebra<Term<F>, IK<F, Term<F>>

interface IKind1<TSelf>
    where TSelf : IKind1<TSelf>
{
    static abstract IKindAlgebra1<TSelf, T, IK<TSelf, T>> FreeFactory<T>();
}

interface IFunctor<F> : IKind1<F>
    where F : IFunctor<F>
{
    static abstract IKindAlgebra1<F, TA, IK<F, TB>> FunctorMapAlgebra<TA, TB>(Func<TA, TB> f);
}

interface INatureTransform<F, G>
    where F : IFunctor<F>
    where G : IFunctor<G>
{
    IKindAlgebra1<F, TI, TO> Apply<TI, TO>(IKindAlgebra1<G, TI, TO> func);
}

//interface IK2<F, TA, TB> : IK<TK<F, TA>, TB>
//{
//}

interface IMonad<F> : IFunctor<F>
    where F : IMonad<F>
{
    static abstract IK<F, A> Pure<A>(A a);
    static abstract IKindAlgebra1<F, TA, IK<F, TB>> MonadSelectManyAlgebra<TA, TB>(Func<TA, IK<F, TB>> f);
}

struct FreeFactory<TKind, T>
    where TKind : IKind1<TKind>
{
    public static IKindAlgebra1<TKind, T, IK<TKind, T>> Factory { get; } = TKind.FreeFactory<T>();
}

// F a
// algebra a b :: f a -> b
//  profunctor instance

// IK<F, A> :: forall R. IAlgebra<F, A, R> -> R

// f a = forall r. f_alg a r -> r

// functor : forall a, b. (a -> b) -> f a -> f b
//           forall a, b. (a -> b) -> alg a (f b)  

// natural transform f g = forall a. f a -> g a
//               ~ forall a. f_alg a (g a)

interface IKindAlgebra1<out TKind, in TI, out TO>
    where TKind : IKind1<TKind>
{
    IKindAlgebra1<TKind, TS, TR> DiMap<TS, TR>(Func<TS, TI> f, Func<TO, TR> g);
}

readonly struct IdFunc<T> : IStaticFunc<IdFunc<T>, T, T>
{
    public static T Apply(T source) => source;
}

// for Kind 
// static IAlgebra<TI, TO> Proj(IFunctorAlgebra<Kind, TI, TO> algebra)

//  given TO. exist TAlgebra : IAlgebra<TI, TO>
// forall r. (forall t. t : IAlgebra<ti, to> -> r) -> r

interface IK<in TKind, out TI>
    where TKind : IKind1<TKind>
{
    TO Evaluate<TO>(IKindAlgebra1<TKind, TI, TO> algebra);

    IK<TKind, TR> Select<TR>(Func<TI, TR> f)
        => Evaluate(TKind.FreeFactory<TR>().DiMap(f, static x => x));
}

interface ITermFactory<TKind> : IKindAlgebra1<TKind, Term<TKind>, Term<TKind>>
    where TKind : IKind1<TKind>
{
}

sealed record class Term<TKind>(IK<TKind, Term<TKind>> Value)
    where TKind : IKind1<TKind>
{
    public static IKindAlgebra1<TKind, Term<TKind>, Term<TKind>> TermFactory { get; } = TKind.FreeFactory<Term<TKind>>()
        .DiMap<Term<TKind>, Term<TKind>>(
            static x => x,
            static e => new(e)
        );
}

static class ReprExtension
{
    public static Term<TKind> Fix<TKind, T>(this IK<TKind, Term<TKind>> e)
        where TKind : IKind1<TKind>
        => new(e);
}

static class Algebra
{
    public static IKindAlgebra1<TKind, T, IK<TKind, T>> FreeFactory<TKind, T>()
        where TKind : IKind1<TKind>
        => TKind.FreeFactory<T>();

    public static IKindAlgebra1<F, T, IK<F, T>> FreeFactory2<F, T>()
        where F : IFunctor<F>
        => F.FunctorMapAlgebra<T, T>(static x => x);

    public static IK<TF, TB> Select<TF, TA, TB, TFA>(this TFA fa, Func<TA, TB> f)
        where TF : IFunctor<TF>
        where TFA : IK<TF, TA>
        => fa.Evaluate(TF.FunctorMapAlgebra(f));

    public static IK<TF, TB> SelectMany<TF, TA, TB>(this IK<TF, TA> fa, Func<TA, IK<TF, TB>> f)
        where TF : IMonad<TF>
        => fa.Evaluate(TF.MonadSelectManyAlgebra(f));
}
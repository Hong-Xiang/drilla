namespace FunctionalExperiment.RecursiveScheme;

static class Option
{
    public sealed class Kind : IMonad<Kind>
    {
        public static IKindAlgebra1<Kind, T, IK<Kind, T>> FreeFactory<T>()
            // => OptionFreeFactory<T>.Instance;
            => new OptionFunctorMapAlgebra<T, T>(static x => x);


        public static IKindAlgebra1<Kind, TA, IK<Kind, TB>> FunctorMapAlgebra<TA, TB>(Func<TA, TB> f)
            => new OptionFunctorMapAlgebra<TA, TB>(f);

        public static IK<Kind, A> Pure<A>(A a)
            // => FreeFactory<A>().Project().Some(a);
            => new Some<A>(a);
        public static IKindAlgebra1<Kind, TA, IK<Kind, TB>> MonadSelectManyAlgebra<TA, TB>(
            Func<TA, IK<Kind, TB>> functor)
            => new OptionMonadSelectManyAlgebra<TA, TB>(functor);
    }


    public static IOptionAlgebra<TI, TO> Project<TI, TO>(this IKindAlgebra1<Kind, TI, TO> algebra)
        => (IOptionAlgebra<TI, TO>)algebra;

    public static IK<Kind, T> Some<T>(T value) => OptionFreeFactory<T>.Instance.Some(value);
    public static IK<Kind, T> None<T>() => OptionFreeFactory<T>.Instance.None();
}

interface IOptionAlgebra<in TI, out TO> : IKindAlgebra1<Option.Kind, TI, TO>
{
    TO None();
    TO Some(TI value);

    IKindAlgebra1<Option.Kind, TS, TR> IKindAlgebra1<Option.Kind, TI, TO>.DiMap<TS, TR>(Func<TS, TI> f,
        Func<TO, TR> g)
        => new OptionDiMapAlgebra<TS, TI, TO, TR>(this, f, g);
}

sealed record class OptionFunctorMapAlgebra<TA, TB>(Func<TA, TB> Func) : IOptionAlgebra<TA, IK<Option.Kind, TB>>
{
    public IK<Option.Kind, TB> None()
        => new None<TB>();

    public IK<Option.Kind, TB> Some(TA value)
        => new Some<TB>(Func(value));
}

sealed record class OptionMonadSelectManyAlgebra<TA, TB>(Func<TA, IK<Option.Kind, TB>> Func)
    : IOptionAlgebra<TA, IK<Option.Kind, TB>>
{
    public IK<Option.Kind, TB> None()
        => new None<TB>();

    public IK<Option.Kind, TB> Some(TA value)
        => Func(value);
}

// fmap :: (a -> b) -> f a -> f b => (a -> b) -> algebra<a, IK<f, b>>
// f a -> defined via free factory : T -> IK<F, T>

// pure :: forall a. a -> f a
// SelectMany :: forall a. f a -> (a -> f b) -> f b
//                         (a -> f b) -> algebra<a, f b>

sealed class OptionDiMapAlgebra<TS, TI, TO, TR>(
    IOptionAlgebra<TI, TO> Base,
    Func<TS, TI> F,
    Func<TO, TR> G) : IOptionAlgebra<TS, TR>
{
    public TR None()
        => G(Base.None());

    public TR Some(TS value)
        => G(Base.Some(F(value)));
}

sealed class OptionFreeFactory<T> : IOptionAlgebra<T, IK<Option.Kind, T>>
{
    public static OptionFreeFactory<T> Instance { get; } = new();

    public IK<Option.Kind, T> None()
        => new None<T>();

    public IK<Option.Kind, T> Some(T value)
        => new Some<T>(value);
}

sealed record class Some<T>(T Value) : IK<Option.Kind, T>
{
    public TO Evaluate<TO>(IKindAlgebra1<Option.Kind, T, TO> algebra)
        => algebra.Project().Some(Value);
}

sealed record class None<T>() : IK<Option.Kind, T>
{
    public TO Evaluate<TO>(IKindAlgebra1<Option.Kind, T, TO> algebra)
        => algebra.Project().None();
}
using System.Runtime.CompilerServices;
using FunctionalExperiment.CompLang;
using FunctionalExperiment.RecursiveScheme;

namespace FunctionalExperiment.IntLang;

static class IntLang
{
    public interface Kind : IKind1<Kind>
    {
        static IKindAlgebra1<Kind, T, IK<Kind, T>> IKind1<Kind>.FreeFactory<T>()
            => FreeFactoryAlgebra<T>.Instance;
    }

    public static IAlgebra<TI, TO> Project<TI, TO>(this IKindAlgebra1<IntLang.Kind, TI, TO> algebra)
        => (IAlgebra<TI, TO>)algebra;
}

// in most cases we only care about - value of IAlgebra -> implementation are singleton
// 2 direction of extension:
//  - via generics, thus IAlgebra<int, int> -> eval, IAlgebra<string, string> pretty printer
//  - via subtype, ideally via default interface -> only works for 

interface IAlgebra<in TI, out TO> : IKindAlgebra1<IntLang.Kind, TI, TO>
{
    TO LitInt(int value);
    TO Add(TI a, TI b);
    TO Mul(TI a, TI b);

    IKindAlgebra1<IntLang.Kind, TS, TR> IKindAlgebra1<IntLang.Kind, TI, TO>.DiMap<TS, TR>(
        Func<TS, TI> f,
        Func<TO, TR> g)
        => new ProSelectAlgebra<TS, TI, TO, TR>(this, f, g);
}

interface IFreeFactoryAlgebra<T, in TS, out TR, TF, TG> : IAlgebra<TS, TR>
    where TF : IStaticFunc<TF, TS, T>
    where TG : IStaticFunc<TG, IK<IntLang.Kind, T>, TR>
{
    TR IAlgebra<TS, TR>.LitInt(int value)
        => TG.Apply(new LitExpr<T>(value));

    TR IAlgebra<TS, TR>.Add(TS a, TS b)
        => TG.Apply(new AddExpr<T>(TF.Apply(a), TF.Apply(b)));

    TR IAlgebra<TS, TR>.Mul(TS a, TS b)
        => TG.Apply(new MulExpr<T>(TF.Apply(a), TF.Apply(b)));
}

sealed class
    FreeFactoryAlgebra<T> : IFreeFactoryAlgebra<T, T, IK<IntLang.Kind, T>, IdFunc<T>, IdFunc<IK<IntLang.Kind, T>>>
{
    public static FreeFactoryAlgebra<T> Instance { get; } = new();
}

sealed class ProSelectAlgebra<TS, TI, TO, TR>(
    IAlgebra<TI, TO> source,
    Func<TS, TI> f,
    Func<TO, TR> g
) : IAlgebra<TS, TR>
{
    public TR LitInt(int value)
        => g(source.LitInt(value));

    public TR Add(TS a, TS b)
        => g(source.Add(f(a), f(b)));

    public TR Mul(TS a, TS b)
        => g(source.Mul(f(a), f(b)));
}

// Lit a
// Add<Lit a, Lit a>
// Add<Lit, Add<Lit, Lit>>
// ...

// algebra :: IExpr<T> -> T
// which is iso to IAlgebra<T, T>
// given any IExpr<T> -> T

// cata :: (Functor f) => Algebra f a -> Term f -> a
// however in csharp, we can not directly model a cata<F> where F is a higher kind type
// 

// coalgebra :: T -> IExpr<T>
// t -> IExpr<T> -> select co

// Func<T, IExpr<T>> ? ~ Func<T, Factory<TExpr>, IExpr<T>>
// ana :: Func<T, IExpr<T>> -> T -> Fix IExpr

// sealed class IsoAlgebra<T>(Func<IExpr<T>, T> Func) : IAlgebra<T, T>
// {
//     public T LitInt(int value)
//         => Func(new LitExpr<T>(value));
//
//     public T Add(T a, T b)
//         => Func(new AddExpr<T>(a, b));
//
//     public T Mul(T a, T b)
//         => Func(new MulExpr<T>(a, b));
//
//     static Func<IExpr<T>, T> Iso(IAlgebra<T, T> algebra) => e => e.Apply(algebra);
// }

// public interface IExpr<out T>
// {
//     public TR Apply<TR>(IAlgebra<T, TR> algebra);
//     public IExpr<TR> Select<TR>(Func<T, TR> f);
// }

// F a
// carrier type forall a -> F a
// map : (a -> b) -> F a -> F b
// all observations over F a:
// F a ~ forall b Algebra<a, b> -> b

// interface IFreeFactory
// {
//     IAlgebra<T, IExpr<T>> Factory<T>();
// }

// forall a. IAlgebra<a, f a>
// ~ forall a, exist t = f a, IAlgebra<a, t>
// ~ forall a, <r>(<f >(Func<IAlgebra<a, f a>, r>) -> r)

// interface IAlgebraFactoryExistEncoding<T>
// {
//     interface IGenericExistEncoding<out TResult>
//     {
//         TResult Apply<TA>(IAlgebra<T, TA> algebra) where TA : IExpr<TA>;
//     }
//
//     TResult Exist<TResult>(IGenericExistEncoding<TResult> algebra);
// }
//
// sealed class Functor(IFreeFactory FreeFactory)
// {
//     public IExpr<TR> Map<TS, TR>(IExpr<TS> e, Func<TS, TR> f)
//         => e.Apply(new FunctorAlgebra<TS, TR>(FreeFactory.Factory<TR>(), f));
// }

// forall a. IAlgebra<a, F a> => derive map
// F a ~ forall b, IAlgebra<a, b> -> b

// interface K<F, T>

// K<F, a> ~ F a

// interface FreeFactory
// {
//     IAlgebra<IExpr<T>, T> FactoryOf<T>();
// }
//
// sealed class FunctorAlgebra<TS, TR>(IAlgebra<TR, IExpr<TR>> factory, Func<TS, TR> Func) : IAlgebra<TS, IExpr<TR>>
// {
//     public IExpr<TR> LitInt(int value)
//         => factory.LitInt(value);
//
//     public IExpr<TR> Add(TS a, TS b)
//         => factory.Add(Func(a), Func(b));
//
//     public IExpr<TR> Mul(TS a, TS b)
//         => factory.Mul(Func(a), Func(b));
// }

// // TExpr<T> : IExpr<T>
// // Select :: Func<T, TR>  -> TExpr<T> -> TExpr<TR>
//
// interface IKind<F, T>
//     where T : allows ref struct
// {
// }
//
// static class Foo
// {
//     struct K<F, T>
//     {
//     }
//
//     readonly ref struct KRef<T>(T value)
//     {
//         public T Value { get; } = value;
//     }
//
//     interface Functor<F>
//     {
//         ref K<F, TR> Select<TS, TR>(ref K<F, TS> source, Func<TS, TR> f);
//     }
//
//     ref struct G<T> : IKind<GK, T>
//         where T : allows ref struct
//     {
//         public T Value { get; }
//     }
//
//     class GK
//     {
//         public static ref G<T> Prj<TGT, T>(ref TGT value) where TGT : struct, IKind<GK, T>, allows ref struct
//             => ref Unsafe.As<TGT, G<T>>(ref value);
//     }
//
//     ref struct S
//     {
//         public int Value { get; }
//     }
//
//     static void Bar(ref S s)
//     {
//     }
// }
//
// IExpr<T> initial algebra
// IExpr<TA> ~ IExpr<TB> using IAlgebra<TA, TB> ?

// cata :: IAlgebra<TA, TA> -> Fix IExpr -> TA

sealed record class LitExpr<TI>(int Value) : IK<IntLang.Kind, TI>
{
    public TO Evaluate<TO>(IKindAlgebra1<IntLang.Kind, TI, TO> algebra)
        => algebra.Project().LitInt(Value);
}

sealed record class AddExpr<T>(T A, T B) : IK<IntLang.Kind, T>
{
    public TO Evaluate<TO>(IKindAlgebra1<IntLang.Kind, T, TO> algebra)
        => algebra.Project().Add(A, B);
}

sealed record class MulExpr<T>(T A, T B) : IK<IntLang.Kind, T>
{
    public TO Evaluate<TO>(IKindAlgebra1<IntLang.Kind, T, TO> algebra)
        => algebra.Project().Mul(A, B);
}

// public interface IFix<TFix>
// {
//     static abstract TFix Fix<TExpr>(TExpr e) where TExpr : IExpr<TFix>;
// }

interface IEvalAlgebra<TSelf> : IAlgebra<int, int>
    where TSelf : IEvalAlgebra<TSelf>
{
    int IAlgebra<int, int>.LitInt(int value) => value;
    int IAlgebra<int, int>.Add(int a, int b) => a + b;
    int IAlgebra<int, int>.Mul(int a, int b) => a * b;
}

// interface IFactory<TLifted> : IAlgebra<TLifted, TLifted>
//     where TLifted : IFix<TLifted>
// {
//     TLifted IAlgebra<TLifted, TLifted>.LitInt(int value)
//         => TLifted.Fix(new LitExpr<TLifted>(value));
//
//     TLifted IAlgebra<TLifted, TLifted>.Add(TLifted a, TLifted b)
//         => TLifted.Fix(new AddExpr<TLifted>(a, b));
//
//     TLifted IAlgebra<TLifted, TLifted>.Mul(TLifted a, TLifted b)
//         => TLifted.Fix(new MulExpr<TLifted>(a, b));
// }

// sealed class Cata<T>(IAlgebra<T, T> Folder)
// {
//     public T Fold<TFix>(TFix e)
//         where TFix : ILift<TFix>
//         => e.Select(Fold).Apply(Folder);
// }
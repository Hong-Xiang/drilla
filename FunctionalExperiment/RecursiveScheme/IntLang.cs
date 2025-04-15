using FunctionalExperiment.CompLang;

namespace FunctionalExperiment.IntLang;

public interface IAlgebra<TI, TO>
{
    TO LitInt(int value);
    TO Add(TI a, TI b);
    TO Mul(TI a, TI b);
}

public interface IExpr<T>
{
    public TR Apply<TR>(IAlgebra<T, TR> algebra);
    public IExpr<TR> Select<TR>(Func<T, TR> f);
}

sealed record class LitExpr<TI>(int Value) : IExpr<TI>
{
    public TRI Apply<TRI>(IAlgebra<TI, TRI> algebra)
        => algebra.LitInt(Value);

    public IExpr<TR> Select<TR>(Func<TI, TR> f)
        => new LitExpr<TR>(Value);
}

sealed record class AddExpr<T>(T A, T B) : IExpr<T>
{
    public TR Apply<TR>(IAlgebra<T, TR> algebra)
        => algebra.Add(A, B);

    public IExpr<TR> Select<TR>(Func<T, TR> f)
        => new AddExpr<TR>(f(A), f(B));
}

sealed record class MulExpr<T>(T A, T B) : IExpr<T>
{
    public TR Apply<TR>(IAlgebra<T, TR> algebra)
        => algebra.Mul(A, B);

    public IExpr<TR> Select<TR>(Func<T, TR> f)
        => new MulExpr<TR>(f(A), f(B));
}

public interface IFix<TFix> : IExpr<TFix>
    where TFix : IExpr<TFix>
{
    TFix Self();

    TR IExpr<TFix>.Apply<TR>(IAlgebra<TFix, TR> algebra)
        => Self().Apply(algebra);

    IExpr<TR> IExpr<TFix>.Select<TR>(Func<TFix, TR> f)
        => Self().Select(f);

    static abstract TFix Create(IExpr<TFix> e);
}

interface IFactory<TFix> : IAlgebra<TFix, TFix>
    where TFix : IFix<TFix>
{
    TFix IAlgebra<TFix, TFix>.LitInt(int value)
        => new LitExpr<TFix>(value).Fix();

    TFix IAlgebra<TFix, TFix>.Add(TFix a, TFix b)
        => new AddExpr<TFix>(a, b).Fix();

    TFix IAlgebra<TFix, TFix>.Mul(TFix a, TFix b)
        => new MulExpr<TFix>(a, b).Fix();
}

sealed class Cata<T>(IAlgebra<T, T> Folder)
{
    public T Fold<TFix>(TFix e)
        where TFix : IFix<TFix>
        => e.Select(Fold).Apply(Folder);
}

public static class IntLang
{
    public static TFix Fix<TFix>(this IExpr<TFix> e)
        where TFix : IFix<TFix>
        => TFix.Create(e);

    public static T Fold<TFix, T>(this TFix e, IAlgebra<T, T> algebra)
        where TFix : IFix<TFix>
        => new Cata<T>(algebra).Fold(e);
}
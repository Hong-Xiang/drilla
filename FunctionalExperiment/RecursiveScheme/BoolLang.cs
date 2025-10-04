namespace FunctionalExperiment.BoolLang;

public interface IAlgebra<TI, TO>
{
    TO LitBool(bool value);
    TO And(TI a, TI b);
}

public interface IExpr<T>
{
    public TR Apply<TR>(IAlgebra<T, TR> algebra);
    public IExpr<TR> Select<TR>(Func<T, TR> f);
}

sealed record class LitBoolExpr<T>(bool Value) : IExpr<T>
{
    public TR Apply<TR>(IAlgebra<T, TR> algebra)
        => algebra.LitBool(Value);

    public IExpr<TBR> Select<TBR>(Func<T, TBR> g)
        => new LitBoolExpr<TBR>(Value);
}

sealed record class AndExpr<TB>(TB A, TB B) : IExpr<TB>
{
    public TRB Apply<TRB>(IAlgebra<TB, TRB> algebra)
        => algebra.And(A, B);

    public IExpr<TBR> Select<TBR>(Func<TB, TBR> g)
        => new AndExpr<TBR>(g(A), g(B));
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

public interface ICata<T>
{
    IAlgebra<T, T> Folder { get; }

    public T Fold<TFix>(TFix e)
        where TFix : IFix<TFix>
        => e.Select(Fold).Apply(Folder);
}

interface IFactory<TFix> : IAlgebra<TFix, TFix>
    where TFix : IFix<TFix>
{
    TFix IAlgebra<TFix, TFix>.LitBool(bool value)
        => new LitBoolExpr<TFix>(value).Fix();

    TFix IAlgebra<TFix, TFix>.And(TFix a, TFix b)
        => new AndExpr<TFix>(a, b).Fix();
}

public static class IntLang
{
    public static TFix Fix<TFix>(this IExpr<TFix> e)
        where TFix : IFix<TFix>
        => TFix.Create(e);
}
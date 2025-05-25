namespace FunctionalExperiment.RecursiveScheme.BaseEncoding;

interface IRecrusive<TCarrier>
{
    ISyntax<TCarrier, TCarrier> Project(TCarrier value);
    ISyntax<TCarrier, TResult> Select<TSource, TResult>(ISyntax<TCarrier, TSource> syntax, Func<TSource, TResult> f);

    TA Fold<TA>(Func<ISyntax<TCarrier, TA>, TA> folder, TCarrier value)
    {
        return folder(this.Select(Project(value), (v) => Fold(folder, v)));
    }
}

sealed record class GenericSyntax<TBrand, TValue>(TValue Value)
{
}

interface ISyntax<TCarrier, T>
{
}

interface ISemantic<TA, TB>
{
}

interface IBase2<TCarrier>
{
    TResult Project<TResult>(TCarrier value, ISemantic<TCarrier, TResult> algebra);

    TSyntaxResult Select<TSource, TResult, TSyntaxResult>( );
}
namespace FunctionalExperiment.RecursiveScheme.Generic;

interface IIntLangAlgebra<TSelf, TI, TO>
    where TSelf : IIntLangAlgebra<TSelf, TI, TO>
{
    TO LitInt(int value);

    TO Add<TA, TB>(TA a, TB b)
        where TA : TI
        where TB : TI;
}

interface IExpr<TI>
{
    TO Apply<TAlgebra, TO>(TAlgebra algebra) where TAlgebra : IIntLangAlgebra<TAlgebra, TI, TO>;
    IExpr<TR> Select<TR>(Func<TI, TR> f);
}

sealed record class LitE<T>(int Value)
{
}

sealed record class AddE<T, TA, TB>(TA a, TB b)
    where TA : T
    where TB : T
{
}
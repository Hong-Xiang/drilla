namespace FunctionalExperiment.RecursiveScheme.Sequence;

interface IAlgebra<in TEI, out TEO, in TSI, out TSO>
{
    TEO Nil();
    TSO Cons(TEI head, TSI tail);
}

interface IElement<TEI, TSI>
{
    TEO Evaluate<TEO, TSO>(IAlgebra<TEI, TSI, TEO, TSO> algebra);
}

interface ISequence<TEI, TSI>
{
    TSO Evaluate<TEO, TSO>(IAlgebra<TEI, TSI, TEO, TSO> algebra);
}

// a -> f a ~ a -> (forall r. alg a r -> r)

sealed class Cata()
{
}

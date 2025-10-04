using FunctionalExperiment.Kind;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FunctionalExperiment.Free;

// Free maps f :: * -> * to a new * -> *

sealed class Free<TK> : IFunctor<Free<TK>>
    where TK : IFunctor<TK>
{
    public static Free<TK> Instance { get; } = new();

    public IAlgebra1<Free<TK>, TS, IK<Free<TK>, TR>> FunctorMap<TS, TR>(Func<TS, TR> f)
    {
        throw new NotImplementedException();
    }
}

interface IFreeAlgebra<TK, in TI, out TO> 
    : IAlgebra1<Free<TK>, TI, TO>
    where TK : IFunctor<TK>
{
}

sealed class LiftF<TF> : INaturalTransform<TF, Free<TF>>
    where TF : IFunctor<TF>
{
    public IK<Free<TF>, T> Apply<T>(IK<TF, T> value)
    {
        throw new NotImplementedException();
    }
}


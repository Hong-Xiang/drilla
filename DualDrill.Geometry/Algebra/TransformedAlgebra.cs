using MathNet.Numerics.LinearAlgebra;

namespace DualDrill.Geometry.Algebra;

public interface IAlgebraBasisTransform<TSource, TTarget>
    where TSource : IAlgebra<TSource>
    where TTarget : IAlgebra<TTarget>
{
    static abstract Element<TTarget> TransformSourceToTarget(Basis sourceBase);
    static abstract Element<TSource> TransformTargetToSource(Basis targetBase);
}

// auto fill 0-vector and k-vector basis (k > 1) transform
public sealed class AlgebraTransformFromVectorBasisTransform<TSource, TTarget, TVectorTransform>
    : IAlgebraBasisTransform<TSource, TTarget>
    where TSource : IAlgebra<TSource>
    where TTarget : IAlgebra<TTarget>
    where TVectorTransform : IAlgebraBasisTransform<TSource, TTarget>
{
    public static Element<TTarget> TransformSourceToTarget(Basis sourceBase)
    {
        var bc = BasisCount(sourceBase);
        if (bc == 0)
        {
            var v = Algebra.VB.Dense(Algebra.GeometryDimension<TTarget>());
            v[0] = 1.0f;
            return new(v);
        }
        if (bc == 1)
        {
            return TVectorTransform.TransformSourceToTarget(sourceBase);
        }
        Element<TTarget>? result = null;
        for (var i = 0; i < Algebra.GeometryDimension<TSource>(); i++)
        {
            if (Algebra.IsSet(sourceBase, i))
            {
                var b = (Basis)(1 << i);
                var t = TVectorTransform.TransformSourceToTarget(b);
                if (result is null)
                {
                    result = t;
                }
                else
                {
                    result = result | t;
                }
            }
        }
        if (!result.HasValue)
        {
            throw new Exception("Unexpected null result");
        }
        return result.Value;
    }

    public static Element<TSource> TransformTargetToSource(Basis targetBase)
    {
        var bc = BasisCount(targetBase);
        if (bc == 0)
        {
            var v = Algebra.VB.Dense(Algebra.GeometryDimension<TTarget>());
            v[0] = 1.0f;
            return new(v);
        }
        if (bc == 1)
        {
            return TVectorTransform.TransformTargetToSource(targetBase);
        }
        Element<TSource>? result = null;
        for (var i = 0; i < Algebra.GeometryDimension<TTarget>(); i++)
        {
            if (Algebra.IsSet(targetBase, i))
            {
                var b = (Basis)(1 << i);
                var t = TVectorTransform.TransformTargetToSource(b);
                if (result.HasValue)
                {
                    result = result | t;
                }
                else
                {
                    result = t;
                }
            }
        }
        if (!result.HasValue)
        {
            throw new Exception("Unexpected null result");
        }
        return result.Value;
    }

    static int BasisCount(Basis b)
    {
        var result = 0;
        for (var i = 0; i < Math.Max(TSource.Dimension, TTarget.Dimension); i++)
        {
            if (Algebra.IsSet(b, i))
            {
                result++;
            }
        }
        return result;
    }
}

public sealed class TransformedAlgebra<TSource, TTarget, TTransform> : IAlgebra<TransformedAlgebra<TSource, TTarget, TTransform>>
   where TSource : IAlgebra<TSource>
   where TTarget : IAlgebra<TTarget>
   where TTransform : IAlgebraBasisTransform<TSource, TTarget>
{
    public static int Dimension => TTarget.Dimension;

    public static Element<TransformedAlgebra<TSource, TTarget, TTransform>> GeometricProduct(Basis l, Basis r)
    {
        var vl = TTransform.TransformTargetToSource(l);
        var vr = TTransform.TransformTargetToSource(r);
        var vs = vl * vr;
        var result = Algebra.VB.Dense(Algebra.GeometryDimension<TTarget>());
        for (var i = 0; i < Algebra.GeometryDimension<TSource>(); i++)
        {
            if (vs.Values[i] != 0.0f)
            {
                result += TTransform.TransformSourceToTarget((Basis)i).Values * vs.Values[i];
            }
        }
        return new(result);
    }

    public static Element<TransformedAlgebra<TSource, TTarget, TTransform>> HodgeStar(Basis b)
    {
        var vb = TTransform.TransformTargetToSource(b);
        var vs = !vb;
        var result = Algebra.VB.Dense(Algebra.GeometryDimension<TTarget>());
        for (var i = 0; i < Algebra.GeometryDimension<TSource>(); i++)
        {
            if (vs.Values[i] != 0.0f)
            {
                result += TTransform.TransformSourceToTarget((Basis)i).Values * vs.Values[i];
            }
        }
        return new(result);
    }

    public static float InnerProduct(Basis l, Basis r)
    {
        var vl = TTransform.TransformTargetToSource(l);
        var vr = TTransform.TransformTargetToSource(r);
        return vl % vr;
    }
}

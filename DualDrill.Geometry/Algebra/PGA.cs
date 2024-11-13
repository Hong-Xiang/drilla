namespace DualDrill.Geometry.Algebra;

using System.Diagnostics;
using System.Numerics;
using CGA3ToPGA3Algebra = TransformedAlgebra<CGA3, PGA3, CGA3ToPGA3>;
using PGA3ToPGA2Algebra = TransformedAlgebra<PGA3, PGA2, PGA3ToPGA2>;

sealed class CGA3ToPGA3 : IAlgebraBasisTransform<CGA3, PGA3>
{
    public static Element<PGA3> TransformSourceToTarget(Basis sourceBase)
    {
        return Algebra.Base<PGA3>(sourceBase & Algebra.BasisI<PGA3>());
    }


    public static Element<CGA3> TransformTargetToSource(Basis targetBase)
        => Algebra.Base<CGA3>(targetBase);
}

public sealed class PGA3 : IAlgebra<PGA3>
{
    public static int Dimension => 4;

    public static Element<PGA3> GeometricProduct(Basis l, Basis r)
        => new(CGA3ToPGA3Algebra.GeometricProduct(l, r).Values);

    public static Element<PGA3> HodgeStar(Basis b)
        => new(CGA3ToPGA3Algebra.HodgeStar(b).Values);


    public static float InnerProduct(Basis l, Basis r)
        => CGA3ToPGA3Algebra.InnerProduct(l, r);

    public static Element<PGA3> Point(float x, float y, float z)
    {
        return x * Algebra.Base<PGA3>(Basis.e1) +
               y * Algebra.Base<PGA3>(Basis.e2) +
               z * Algebra.Base<PGA3>(Basis.e3) +
               Algebra.Base<PGA3>(Basis.e4);
    }

    public static Vector3 Point(Element<PGA3> e)
    {
        var w = e[Basis.e4];
        Debug.Assert(w != 0.0f);
        return new(e[Basis.e1] / w, e[Basis.e2] / w, e[Basis.e3] / w);

    }
}
sealed class PGA3ToPGA2 : IAlgebraBasisTransform<PGA3, PGA2>
{
    public static Element<PGA2> TransformSourceToTarget(Basis sourceBase)
    {
        var b = Basis.s;
        b |= (sourceBase & Basis.e1);
        b |= (sourceBase & Basis.e2);
        b |= (sourceBase & Basis.e4) != 0 ? Basis.e3 : 0;
        return Algebra.Base<PGA2>(b);
    }

    public static Element<PGA3> TransformTargetToSource(Basis targetBase)
    {
        var b3 = targetBase & Basis.e1;
        b3 |= targetBase & Basis.e2;
        b3 |= ((targetBase & Basis.e3) != 0) ? Basis.e4 : 0;
        return Algebra.Base<PGA3>(b3);
    }
}

public sealed class PGA2 : IAlgebraSignature<PGA2>, IAlgebra<PGA2>
{
    public static int Dimension => 3;

    public static int Sign(int dim) => dim == 0 ? 0 : 1;

    public static Element<PGA2> Point(float x, float y)
    {
        return -x * Algebra.Base<PGA2>(Basis.e3 | Basis.e1)
               + y * Algebra.Base<PGA2>(Basis.e1 | Basis.e2)
               + Algebra.Base<PGA2>(Basis.e2 | Basis.e3);
    }

    public static Vector2 Point(Element<PGA2> e)
    {
        var w = e[Basis.e2 | Basis.e3];
        Debug.Assert(w != 0.0f);
        return new(-e[Basis.e1 | Basis.e3] / w, e[Basis.e1 | Basis.e2] / w);
    }
    public static Vector2 Direction(Element<PGA2> e)
    {
        return new(-e[Basis.e1 | Basis.e3], e[Basis.e1 | Basis.e2]);
    }

    public static Element<PGA2> HodgeStar(Basis b)
        => new(PGA3ToPGA2Algebra.HodgeStar(b).Values);

    public static Element<PGA2> GeometricProduct(Basis l, Basis r)
        => new(PGA3ToPGA2Algebra.GeometricProduct(l, r).Values);


    public static float InnerProduct(Basis l, Basis r)
        => PGA3ToPGA2Algebra.InnerProduct(l, r);
}


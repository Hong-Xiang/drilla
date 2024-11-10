namespace DualDrill.Geometry.Algebra;

using System.Diagnostics;
using CGA3ToPGA3Algebra = TransformedAlgebra<CGA3, PGA3, CGA3ToPGA3>;

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
}




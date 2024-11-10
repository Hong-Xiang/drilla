namespace DualDrill.Geometry.Algebra;
using CGA3T = TransformedAlgebra<CGA3Diagonal, CGA3, AlgebraTransformFromVectorBasisTransform<CGA3Diagonal, CGA3, CGA3Transform>>;

public sealed class CGA3Diagonal : IAlgebraSignature<CGA3Diagonal>, IAlgebra<CGA3Diagonal>
{
    public static int Dimension => 5;

    public static Element<CGA3Diagonal> GeometricProduct(Basis l, Basis r)
        => new(DiagonalAlgebra<CGA3Diagonal>.GeometricProduct(l, r).Values);

    public static Element<CGA3Diagonal> HodgeStar(Basis b)
        => new(DiagonalAlgebra<CGA3Diagonal>.HodgeStar(b).Values);

    public static float InnerProduct(Basis l, Basis r)
        => DiagonalAlgebra<CGA3Diagonal>.InnerProduct(l, r);

    public static int Sign(int dim)
        => dim == 4 ? -1 : 1;
}

public sealed class CGA3Transform : IAlgebraBasisTransform<CGA3Diagonal, CGA3>
{
    public static Element<CGA3> TransformSourceToTarget(Basis sourceBase)
    {
        return sourceBase switch
        {
            Basis.s => Algebra.Base<CGA3>(Basis.s),
            Basis.e1 => Algebra.Base<CGA3>(Basis.e1),
            Basis.e2 => Algebra.Base<CGA3>(Basis.e2),
            Basis.e3 => Algebra.Base<CGA3>(Basis.e3),
            Basis.e4 => -Algebra.Base<CGA3>(Basis.e4) + 0.5f * Algebra.Base<CGA3>(Basis.e5),
            Basis.e5 => Algebra.Base<CGA3>(Basis.e4) + 0.5f * Algebra.Base<CGA3>(Basis.e5),
            _ => throw new NotImplementedException()
        };
    }

    public static Element<CGA3Diagonal> TransformTargetToSource(Basis targetBase)
    {
        return targetBase switch
        {
            Basis.s => Algebra.Base<CGA3Diagonal>(Basis.s),
            Basis.e1 => Algebra.Base<CGA3Diagonal>(Basis.e1),
            Basis.e2 => Algebra.Base<CGA3Diagonal>(Basis.e2),
            Basis.e3 => Algebra.Base<CGA3Diagonal>(Basis.e3),
            Basis.e4 => 0.5f * (Algebra.Base<CGA3Diagonal>(Basis.e5) - Algebra.Base<CGA3Diagonal>(Basis.e4)),
            Basis.e5 => Algebra.Base<CGA3Diagonal>(Basis.e5) + Algebra.Base<CGA3Diagonal>(Basis.e4),
            _ => throw new NotImplementedException()
        };
    }
}

public sealed class CGA3 : IAlgebra<CGA3>
{
    public static int Dimension => 5;

    public static Element<CGA3> GeometricProduct(Basis l, Basis r)
        => new(CGA3T.GeometricProduct(l, r).Values);

    public static Element<CGA3> HodgeStar(Basis b)
        => new(CGA3T.HodgeStar(b).Values);

    public static float InnerProduct(Basis l, Basis r)
        => CGA3T.InnerProduct(l, r);
}


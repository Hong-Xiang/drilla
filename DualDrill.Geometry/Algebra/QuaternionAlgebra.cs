namespace DualDrill.Geometry.Algebra;

public sealed class QuaternionAlgebra : IAlgebraSignature<QuaternionAlgebra>, IAlgebra<QuaternionAlgebra>
{
    public static int Dimension => 3;

    public static int Sign(int dim) => 1;
    public static Element<QuaternionAlgebra> GeometricProduct(Basis l, Basis r)
        => new(DiagonalAlgebra<QuaternionAlgebra>.GeometricProduct(l, r).Values);

    public static Element<QuaternionAlgebra> HodgeStar(Basis b)
        => new(DiagonalAlgebra<QuaternionAlgebra>.HodgeStar(b).Values);

    public static float InnerProduct(Basis l, Basis r)
        => DiagonalAlgebra<QuaternionAlgebra>.InnerProduct(l, r);
}

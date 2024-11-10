namespace DualDrill.Geometry.Algebra;

public sealed class ComplexAlgebra : IAlgebraSignature<ComplexAlgebra>, IAlgebra<ComplexAlgebra>
{
    public static int Dimension => 2;

    public static Element<ComplexAlgebra> GeometricProduct(Basis l, Basis r)
        => new(DiagonalAlgebra<ComplexAlgebra>.GeometricProduct(l, r).Values);

    public static Element<ComplexAlgebra> HodgeStar(Basis b)
        => new(DiagonalAlgebra<ComplexAlgebra>.HodgeStar(b).Values);

    public static float InnerProduct(Basis l, Basis r)
        => DiagonalAlgebra<ComplexAlgebra>.InnerProduct(l, r);

    public static int Sign(int dim) => 1;
}

namespace DualDrill.Geometry.Algebra;

// encode k-vector basis into a flat array
// since all permutation of basis are equivalent up to sign
// we use a bitmap to encode baisis, i.e.
// for dim N, the values is a 2^N length array
// e.g. for dim 3, values is a array of length 8
// its index are bitmap of vector basis
// e.g. values[0] are for basis 1 (scalar)
//      values[1] are for basis 0...01 (e0) (1 << 0)

// for basis a, b, result of a | b would be bitwise (+-) a ^ b
// the actual sign is based on permutations of bits in a and b
public interface IAlgebra<TSelf> : IAlgebraSpace<TSelf>
    where TSelf : IAlgebra<TSelf>
{
    public static abstract Element<TSelf> HodgeStar(Basis b);
    //static abstract Element<TSelf> Differential(Basis b);
    public static abstract Element<TSelf> GeometricProduct(Basis l, Basis r);
    public static abstract float InnerProduct(Basis l, Basis r);
}

using System.Runtime.CompilerServices;

namespace DualDrill.Geometry.Algebra;

// Only works for signature with orthogonal basis,
// and with sign = 1 or -1
public sealed class DiagonalAlgebra<TSignature> : IAlgebra<DiagonalAlgebra<TSignature>>
    where TSignature : IAlgebraSignature<TSignature>
{
    public static int Dimension => TSignature.Dimension;
    static readonly int GD = 1 << Dimension;
    static readonly Basis BI = Algebra.BasisI<TSignature>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float InnerProduct(Basis l, Basis r)
    {
        if (l == 0 && r == 0)
        {
            return 1.0f;
        }
        if ((l ^ r) != 0 || (l & r) == 0)
        {
            return 0.0f;
        }
        var result = 1.0f;
        for (var di = 0; di < TSignature.Dimension; di++)
        {
            if (Algebra.IsSet(l, di) && Algebra.IsSet(r, di))
            {
                result *= TSignature.Sign(di);
            }
        }
        return result;
    }


    public static Element<DiagonalAlgebra<TSignature>> GeometricProduct(Basis l, Basis r)
    {
        var e = 1;
        var s = l & r;
        for (var d = 0; d < TSignature.Dimension; d++)
        {
            if (Algebra.IsSet(l, d) && Algebra.IsSet(r, d))
            {
                e *= TSignature.Sign(d);
            }
        }
        for (var da = 0; da < TSignature.Dimension; da++)
        {
            for (var db = 0; db < TSignature.Dimension; db++)
            {
                if (Algebra.IsSet(l, da) && Algebra.IsSet(r, db) && da > db)
                {
                    e *= -1;
                }
            }
        }
        var rv = Algebra.VB.Dense(GD);
        rv[(int)(l ^ r)] = e;
        return new(rv);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Element<DiagonalAlgebra<TSignature>> HodgeStar(Basis b)
    {
        // follow https://en.wikipedia.org/wiki/Hodge_star_operator 's Formal definition for k-vectors
        var bi = Algebra.BasisI<TSignature>();
        var rb = bi & (~b);
        var result = Algebra.VB.Dense(GD);
        float s = Algebra.Sign<TSignature>(b, (~b) & bi);
        float d = InnerProduct(b, b);
        result[(int)rb] = s * d;
        return new(result);
    }

}

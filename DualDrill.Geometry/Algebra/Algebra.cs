using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using MNL = MathNet.Numerics.LinearAlgebra;

namespace DualDrill.Geometry.Algebra;

public static class Algebra
{
    internal static readonly MNL.VectorBuilder<float> VB = MNL.Vector<float>.Build;
    internal static readonly MNL.MatrixBuilder<float> MB = MNL.Matrix<float>.Build;
    public static bool IsSet(Basis basis, int dim) => (basis & (Basis)(1 << dim)) != 0;

    public static Basis BasisI<TSpace>() where TSpace : IAlgebraSpace<TSpace>
    {
        uint result = 0;
        for (var i = 0; i < TSpace.Dimension; i++)
        {
            result |= (1u << i);
        }
        return (Basis)result;
    }

    public static int GeometryDimension<TAlgebra>() where TAlgebra : IAlgebraSpace<TAlgebra>
        => 1 << TAlgebra.Dimension;
    public static bool IsValid<TAlgebra>(int dim)
        where TAlgebra : IAlgebra<TAlgebra>
        => dim < TAlgebra.Dimension;
    public static bool IsValid<TAlgebra>(Basis basis)
        where TAlgebra : IAlgebra<TAlgebra>
        => (int)basis < GeometryDimension<TAlgebra>();

    public static int Sign<TAlgebra>(Basis l, Basis r)
        where TAlgebra : IAlgebraSpace<TAlgebra>
    {
        if ((l & r) != 0)
        {
            return 0;
        }
        int sign = 1;
        for (var da = 0; da < TAlgebra.Dimension; da++)
        {
            if (IsSet(l, da))
            {
                for (var db = 0; db <= da; db++)
                {
                    if (IsSet(r, db))
                    {
                        sign *= -1;
                    }
                }
            }
        }
        return sign;
    }

    public static Element<TAlgebra> Base<TAlgebra>(Basis basis)
        where TAlgebra : IAlgebra<TAlgebra>
    {
        Debug.Assert(IsValid<TAlgebra>(basis));
        return AlgebraData<TAlgebra>.BaseKVectors[(int)basis];
    }

    public static Element<TAlgebra> Base<TAlgebra>(int dim)
        where TAlgebra : IAlgebra<TAlgebra>
    {
        Debug.Assert(IsValid<TAlgebra>(dim));
        return Base<TAlgebra>((Basis)(1 << dim));
    }

    public static string Name<TAlgebra>(this Basis b, string scalarName = "")
        where TAlgebra : IAlgebraSpace<TAlgebra>
    {
        var sb = new StringBuilder(TAlgebra.Dimension);
        if (IsScalar(b))
        {
            return scalarName;
        }
        sb.Append("e");
        for (var i = 0; i < TAlgebra.Dimension; i++)
        {
            if (b.HasFlag((Basis)(1 << i)))
            {
                sb.Append(i + 1);
            }
        }
        return sb.ToString();
    }

    public static bool IsScalar(Basis b) => b == 0;

    public static Element<TAlgebra> Create<TAlgebra>(float scalar)
        where TAlgebra : IAlgebra<TAlgebra>
    {
        var values = VB.Dense(GeometryDimension<TAlgebra>());
        values[0] = scalar;
        return new(values);
    }

    public static ImmutableArray<ImmutableArray<Element<TAlgebra>>> GetGeometricProductTable<TAlgebra>()
        where TAlgebra : IAlgebra<TAlgebra>
    {
        var gd = GeometryDimension<TAlgebra>();
        var result = new ImmutableArray<Element<TAlgebra>>[gd];
        for (var i = 0; i < gd; i++)
        {
            var l = Base<TAlgebra>((Basis)i);
            var row = new Element<TAlgebra>[gd];
            for (var j = 0; j < gd; j++)
            {
                var r = Base<TAlgebra>((Basis)j);
                row[j] = l * r;
            }
            result[i] = [.. row];
        }
        return [.. result];
    }
}

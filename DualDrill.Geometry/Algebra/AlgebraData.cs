using System.Collections.Immutable;
using System.Diagnostics;
using System.Numerics;
using MNL = MathNet.Numerics.LinearAlgebra;

namespace DualDrill.Geometry.Algebra;

static class AlgebraData<TAlgebra>
  where TAlgebra : IAlgebra<TAlgebra>
{
    internal static ImmutableArray<Element<TAlgebra>> BaseKVectors = CreateBaseVectors();
    internal static ImmutableArray<Element<TAlgebra>> HodgeStarTable = CreateHodgeStarLookup();
    internal static ImmutableArray<ImmutableArray<Element<TAlgebra>>> GeometricProductTable = CreateGeometricProductTable();
    internal static ImmutableArray<ImmutableArray<float>> InnerProductTable = CreateInnerProductTable();
    static ImmutableArray<Element<TAlgebra>> CreateBaseVectors()
    {
        var result = new Element<TAlgebra>[Algebra.GeometryDimension<TAlgebra>()];
        for (var i = 0; i < result.Length; i++)
        {
            var b = (Basis)i;
            var values = Algebra.VB.Dense(Algebra.GeometryDimension<TAlgebra>());
            if (Algebra.IsScalar(b))
            {
                values[0] = 1.0f;
                result[i] = new(values);
                continue;
            }
            for (var dim = 0; dim < TAlgebra.Dimension; dim++)
            {
                if (!Algebra.IsSet(b, dim))
                {
                    continue;
                }
            }
            values[i] = 1.0f;
            result[i] = new(values);
        }
        return [.. result];
    }


    internal static ImmutableArray<ImmutableArray<float>> CreateInnerProductTable()
    {
        var result = new ImmutableArray<float>[GD];
        for (var i = 0; i < GD; i++)
        {
            var row = new float[GD];
            for (var j = 0; j < GD; j++)
            {
                row[j] = TAlgebra.InnerProduct((Basis)i, (Basis)j);
            }
            result[i] = [.. row];
        }
        return [.. result];

    }

    internal static ImmutableArray<ImmutableArray<Element<TAlgebra>>> CreateGeometricProductTable()
    {
        var result = new ImmutableArray<Element<TAlgebra>>[GD];
        for (var i = 0; i < GD; i++)
        {
            var row = new Element<TAlgebra>[GD];
            for (var j = 0; j < GD; j++)
            {
                row[j] = TAlgebra.GeometricProduct((Basis)i, (Basis)j);
            }
            result[i] = [.. row];
        }
        return [.. result];
    }

    static int GD => Algebra.GeometryDimension<TAlgebra>();

    internal static MNL.Matrix<float> CreateMulMat()
    {
        var result = Algebra.MB.Dense(GD, GD);

        return result;
    }

    internal static ImmutableArray<Element<TAlgebra>> CreateHodgeStarLookup()
    {
        var result = new Element<TAlgebra>[GD];
        for (var i = 0; i < GD; i++)
        {
            result[i] = TAlgebra.HodgeStar((Basis)i);
        }
        return [.. result];
    }
}



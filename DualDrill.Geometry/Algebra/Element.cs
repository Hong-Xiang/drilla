using System.Text;

namespace DualDrill.Geometry.Algebra;

/// <summary>
/// assumes Values.Length == Algebra.Geometric Dimension
/// </summary>
/// <typeparam name="TAlgebra"></typeparam>
/// <param name="Values"></param>
public readonly record struct Element<TAlgebra>
    where TAlgebra : IAlgebra<TAlgebra>
{
    public MathNet.Numerics.LinearAlgebra.Vector<float> Values { get; }

    internal Element(MathNet.Numerics.LinearAlgebra.Vector<float> values)
    {
        Values = values;
    }

    public static implicit operator Element<TAlgebra>(float scalar) => Algebra.Create<TAlgebra>(scalar);

    public float this[Basis b] => Values[(int)b];

    public Element<TAlgebra> this[Element<TAlgebra> m] => (~this) & m;

    public override string ToString()
    {
        var isZero = true;
        var sb = new StringBuilder();
        if (Values[0] != 0)
        {
            sb.Append(Values[0]);
            isZero = false;
        }
        for (int i = 1; i < Values.Count; i++)
        {
            if (Values[i] == 0.0f)
            {
                continue;
            }
            var b = (Basis)i;
            if (!isZero)
            {
                sb.Append(" + ");
            }
            if (Values[i] < 0)
            {
                //sb.Append('(');
                if (Values[i] == -1.0f)
                {
                    sb.Append('-');
                }
                else
                {
                    sb.Append(Values[i]);
                }
                sb.Append(b.Name<TAlgebra>());
                //sb.Append(')');
            }
            else
            {
                if (!(Values[i] == 1.0f))
                {
                    sb.Append(Values[i]);
                }
                sb.Append(b.Name<TAlgebra>());
            }
            isZero = false;
        }
        if (isZero)
        {
            return "0";
        }
        else
        {
            return sb.ToString();
        }
    }
    public static Element<TAlgebra> operator -(Element<TAlgebra> v)
    {
        return new(-v.Values);
    }

    public static Element<TAlgebra> operator +(Element<TAlgebra> a, Element<TAlgebra> b)
    {
        return new(a.Values + b.Values);
    }
    public static Element<TAlgebra> operator -(Element<TAlgebra> a, Element<TAlgebra> b)
    {
        return new(a.Values - b.Values);
    }

    public static Element<TAlgebra> operator ~(Element<TAlgebra> e)
    {
        var gd = Algebra.GeometryDimension<TAlgebra>();
        var result = Algebra.VB.Dense(gd);
        for (var i = 0; i < gd; i++)
        {
            result += AlgebraData<TAlgebra>.HodgeStarTable[i].Values * e.Values[i];
        }
        return new(result);
    }

    public static Element<TAlgebra> operator *(Element<TAlgebra> a, Element<TAlgebra> b)
    {
        var gd = 1 << TAlgebra.Dimension;
        var result = Algebra.VB.Dense(gd);
        for (var i = 0; i < gd; i++)
        {
            for (var j = 0; j < gd; j++)
            {
                result += AlgebraData<TAlgebra>.GeometricProductTable[i][j].Values * a.Values[i] * b.Values[j];
            }
        }
        return new(result);
    }
    public static Element<TAlgebra> operator *(float a, Element<TAlgebra> b)
    {
        return new(a * b.Values);
    }
    public static Element<TAlgebra> operator *(Element<TAlgebra> a, float b)
    {
        return new(a.Values * b);
    }
    public static Element<TAlgebra> operator ^(Element<TAlgebra> a, Element<TAlgebra> b)
    {
        return (~a & b) | (a & ~b);
    }
    public static Element<TAlgebra> operator &(Element<TAlgebra> a, Element<TAlgebra> b)
    {
        return ~((~a) | (~b));
    }
    public static Element<TAlgebra> operator |(Element<TAlgebra> a, Element<TAlgebra> b)
    {
        var gd = Algebra.GeometryDimension<TAlgebra>();
        var result = Algebra.VB.Dense(gd);
        for (var l = 0; l < gd; l++)
        {
            for (var r = 0; r < gd; r++)
            {
                if ((l & r) != 0)
                {
                    continue;
                }
                var rb = l ^ r;
                var s = Algebra.Sign<TAlgebra>((Basis)l, (Basis)r);
                result[rb] += s * a.Values[l] * b.Values[r];
            }
        }
        return new(result);
    }
    public static float operator %(Element<TAlgebra> a, Element<TAlgebra> b)
    {
        var gd = Algebra.GeometryDimension<TAlgebra>();
        var result = 0.0f;
        for (var i = 0; i < gd; i++)
        {
            for (var j = 0; j < gd; j++)
            {
                result += AlgebraData<TAlgebra>.InnerProductTable[i][j] * a.Values[i] * b.Values[j];
            }
        }
        return result;
    }
}

using DualDrill.Geometry.Algebra;
using Microsoft.AspNetCore.Mvc;
using System.Numerics;

namespace DualDrill.Server.Controllers;

using Q = QuaternionAlgebra;
using H = ComplexAlgebra;
using Alg = PGA2;
using CGA3T = TransformedAlgebra<CGA3Diagonal, CGA3, AlgebraTransformFromVectorBasisTransform<CGA3Diagonal, CGA3, CGA3Transform>>;
readonly record struct Line(Vector3 Start, Vector3 End, string color)
{
    public static Line From(Element<PGA2> e, string color)
    {
        return new Line(new Vector3(e.Values[1], e.Values[2], 0.0f),
                        new Vector3(),
                        color);
    }

}

readonly record struct Point(Vector3 Position, string Color)
{
    public static Point From(Element<PGA2> e, string color)
    {
        var p = PGA2.Point(e);
        return new Point(new Vector3(
            p.X,
            p.Y,
            0.0f
        ), color);
    }

    public static Point From(Element<PGA3> e, string color)
    {
        return new(PGA3.Point(e), color);
    }
}

sealed record class EntityModel(Point[] Points, string Text)
{
}

[Route("[controller]")]
public class GADevController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        var table = Algebra.GetGeometricProductTable<Alg>();
        var em = Algebra.Base<CGA3Diagonal>(Basis.e5);
        var ep = Algebra.Base<CGA3Diagonal>(Basis.e4);
        var e4t = 0.5f * (em - ep);
        var e5t = em + ep;
        var r = e4t * e5t;

        var e45 = CGA3T.GeometricProduct(Basis.e4, Basis.e5);

        return View(Algebra.GeometryDimension<Alg>());
    }

    [HttpGet("entity")]
    public IActionResult Entity()
    {
        var px = PGA3.Point(1.0f, 0.0f, 0.0f);
        var py = PGA3.Point(0.0f, 1.0f, 0.0f);
        var pz = PGA3.Point(0.0f, 0.0f, 1.0f);
        var po = PGA3.Point(0.0f, 0.0f, 0.0f);
        var p = 0.5f * px + 0.5f * py;

        var scene = new EntityModel([
            Point.From(px, "red"),
            Point.From(py, "green"),
            Point.From(pz, "yellow"),
            Point.From(p, "blue"),
            Point.From(po, "gray"),
        ], (px | py | pz).ToString());
        return View(scene);
    }

    public static string GetName(Basis b) => b.Name<Alg>("1");
    public static string GetName(int b) => ((Basis)b).Name<Alg>("1");

    public static string PD(int a, int b)
    {
        return Algebra.GetGeometricProductTable<Alg>()[a][b].ToString();
    }

    public static string Inner(int a, int b)
    {
        return (Algebra.Base<Alg>((Basis)a) % Algebra.Base<Alg>((Basis)b)).ToString();
    }

    public static string Outer(int a, int b)
    {
        return (Algebra.Base<Alg>((Basis)a) | Algebra.Base<Alg>((Basis)b)).ToString();
    }

    public static string Prod(int a, int b)
    {
        return (Algebra.Base<Alg>((Basis)a) * Algebra.Base<Alg>((Basis)b)).ToString();
    }
    public static string Dual(int a)
    {
        return (~Algebra.Base<Alg>((Basis)a)).ToString();
    }
    public static string SignName(float sign) => sign switch
    {
        0 => "0",
        > 0 => "",
        < 0 => "-",
        _ => throw new NotImplementedException(),
    };
}


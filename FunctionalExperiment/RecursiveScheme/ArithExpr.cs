using FunctionalExperiment.RecursiveScheme;

namespace FunctionalExperiment.CompLang;

interface IAlgebra<TII, TIO, TBI, TBO>
    : IntLang.IAlgebra<TII, TIO>
    , BoolLang.IAlgebra<TBI, TBO>
{
    TBO Gt(TII a, TII b);
    TIO Select(TBI cond, TII t, TII f);
}

interface IExprI<TI, TB>
{
    public TRI Apply<TRI, TRB>(IAlgebra<TI, TRI, TB, TRB> algebra);
    public IExprI<TRI, TRB> Select<TRI, TRB>(Func<TI, TRI> f, Func<TB, TRB> g);
}

sealed record class LiftedI<TI, TB>(IK<IntLang.IntLang.Kind, TI> Expr) : IExprI<TI, TB>
{
    public TRI Apply<TRI, TRB>(IAlgebra<TI, TRI, TB, TRB> algebra)
        => Expr.Evaluate(algebra);

    public IExprI<TIR, TBR> Select<TIR, TBR>(Func<TI, TIR> f, Func<TB, TBR> g)
        => Expr.Select(f).Lift<TIR, TBR>();
}

interface IExprB<TI, TB>
{
    public TRB Apply<TRI, TRB>(IAlgebra<TI, TRI, TB, TRB> algebra);
    public IExprB<TIR, TBR> Select<TIR, TBR>(Func<TI, TIR> f, Func<TB, TBR> g);
}

sealed record class LiftedB<TI, TB>(BoolLang.IExpr<TB> Expr) : IExprB<TI, TB>
{
    public TRB Apply<TRI, TRB>(IAlgebra<TI, TRI, TB, TRB> algebra)
        => Expr.Apply(algebra);

    public IExprB<TIR, TBR> Select<TIR, TBR>(Func<TI, TIR> f, Func<TB, TBR> g)
        => Expr.Select(g).Lift<TIR, TBR>();
}

// IExprI<FixExprI, FixExprB>

sealed record class GtExpr<TI, TB>(TI A, TI B) : IExprB<TI, TB>
{
    public TRB Apply<TRI, TRB>(IAlgebra<TI, TRI, TB, TRB> algebra)
        => algebra.Gt(A, B);

    public IExprB<TIR, TBR> Select<TIR, TBR>(Func<TI, TIR> f, Func<TB, TBR> g)
        => new GtExpr<TIR, TBR>(f(A), f(B));
}

sealed class SelectExpr<TI, TB>(TB Cond, TI T, TI F) : IExprI<TI, TB>
{
    public TRI Apply<TRI, TRB>(IAlgebra<TI, TRI, TB, TRB> algebra)
        => algebra.Select(Cond, T, F);

    public IExprI<TIR, TBR> Select<TIR, TBR>(Func<TI, TIR> f, Func<TB, TBR> g)
        => new SelectExpr<TIR, TBR>(g(Cond), f(T), f(F));
}

readonly record struct FixExprI(IExprI<FixExprI, FixExprB> Expr)
    // : IExprI<FixExprI, FixExprB>
    // : IntLang.IFix<FixExprI>
{
    // public TRI Apply<TRI, TRB>(IAlgebra<FixExprI, TRI, FixExprB, TRB> algebra)
    //     => Expr.Apply(algebra);
    //
    // public IExprI<TIR, TBR> Select<TIR, TBR>(Func<FixExprI, TIR> f, Func<FixExprB, TBR> g)
    //     => Expr.Select(f, g);
    // public static FixExprI Fix<TExpr>(TExpr e)
    //     where TExpr : IntLang.IExpr<FixExprI>
    //     => e.Lift().Fix();
}

readonly record struct FixExprB(IExprB<FixExprI, FixExprB> Expr)
    // : IExprB<FixExprI, FixExprB>
    : BoolLang.IFix<FixExprB>
{
    // public TRB Apply<TRI, TRB>(IAlgebra<FixExprI, TRI, FixExprB, TRB> algebra)
    //     => Expr.Apply(algebra);
    //
    // public IExprB<TIR, TBR> Select<TIR, TBR>(Func<FixExprI, TIR> f, Func<FixExprB, TBR> g)
    //     => Expr.Select(f, g);

    public FixExprB Self() => this;

    public static FixExprB Create(BoolLang.IExpr<FixExprB> e)
        => e.Lift<FixExprI, FixExprB>().Fix();
}

sealed record class Cata<TI, TB>(IAlgebra<TI, TI, TB, TB> Folder)
{
    public TI Fold(FixExprI e)
        => e.Expr.Select(Fold, Fold).Apply(Folder);

    public TB Fold(FixExprB e)
        => e.Expr.Select(Fold, Fold).Apply(Folder);
}

sealed class Factory :
    IAlgebra<FixExprI, FixExprI, FixExprB, FixExprB>
  , IntLang.IFreeFactoryAlgebra<FixExprI, FixExprI, FixExprI, IdFunc<FixExprI>, Factory.LiftExprIFunc>
  , BoolLang.IFactory<FixExprB>
{
    struct LiftExprIFunc : IStaticFunc<LiftExprIFunc, IK<IntLang.IntLang.Kind, FixExprI>, FixExprI>
    {
        public static FixExprI Apply(IK<IntLang.IntLang.Kind, FixExprI> value)
            => value.Lift<FixExprI, FixExprB>().Fix();
    }

    // public FixExprI LitInt(int value)
    //     => new LitExpr<FixExprI>(value).Lift().Fix();
    //
    // public FixExprB LitBool(bool value)
    //     => new LitBoolExpr<FixExprI, FixExprB>(value).Fix();
    //
    // public FixExprB And(FixExprB a, FixExprB b)
    //     => new AndExpr<FixExprI, FixExprB>(a, b).Fix();

    public FixExprB Gt(FixExprI a, FixExprI b)
        => new GtExpr<FixExprI, FixExprB>(a, b).Fix();

    // public FixExprI Add(FixExprI a, FixExprI b)
    //     => new AddExpr<FixExprI, FixExprB>(a, b).Fix();
    //
    // public FixExprI Mul(FixExprI a, FixExprI b)
    //     => new MulExpr<FixExprI, FixExprB>(a, b).Fix();

    public FixExprI Select(FixExprB cond, FixExprI t, FixExprI f)
        => new SelectExpr<FixExprI, FixExprB>(cond, t, f).Fix();

    // public FixExprI LitInt(int value)
    //     => Algebra.FreeFactory<IntLang.IntLang.Kind, FixExprI>().Project().LitInt(value).Lift().Fix();
    //
    // public FixExprI Add(FixExprI a, FixExprI b)
    //     => FreeFactory<IntLang.IntLang.Kind, FixExprI>.Factory.Project().Add(a, b).Lift().Fix();
    //
    // public FixExprI Mul(FixExprI a, FixExprI b)
    //     => FreeFactory<IntLang.IntLang.Kind, FixExprI>.Factory.Project().Mul(a, b).Lift().Fix();
}

sealed class EvalAlgebra
    : IAlgebra<int, int, bool, bool>
    , IntLang.IEvalAlgebra<EvalAlgebra>
{
    // public int LitInt(int value) => value;

    public bool LitBool(bool value) => value;

    public bool And(bool a, bool b) => a && b;


    public bool Gt(int a, int b) => a > b;
    // public int Add(int a, int b) => a + b;
    // public int Mul(int a, int b) => a * b;

    public int Select(bool cond, int t, int f)
        => cond ? t : f;
}

sealed class ReprAlgebra : IAlgebra<string, string, string, string>
{
    public string LitInt(int value) => $"{value}";

    public string LitBool(bool value) => value ? "true" : "false";

    public string And(string a, string b) => $"({a} && {b})";

    public string Gt(string a, string b) => $"({a} > {b})";

    public string Add(string a, string b)
        => $"({a} + {b})";

    public string Mul(string a, string b)
        => $"({a} * {b})";

    public string Select(string cond, string t, string f)
        => $"({cond} ? {t} : {f})";
}

static class ArithExpr
{
    public static FixExprI Fix(this IExprI<FixExprI, FixExprB> e) => new(e);
    public static FixExprB Fix(this IExprB<FixExprI, FixExprB> e) => new(e);

    public static IExprI<TI, TB> Lift<TI, TB>(this IK<IntLang.IntLang.Kind, TI> e) =>
        new LiftedI<TI, TB>(e);

    public static IExprB<TI, TB> Lift<TI, TB>(this BoolLang.IExpr<TB> e) =>
        new LiftedB<TI, TB>(e);

    public static IExprI<FixExprI, FixExprB> Unfix(this FixExprI e) => e.Expr;
    public static IExprB<FixExprI, FixExprB> Unfix(this FixExprB e) => e.Expr;

    public static TI Fold<TI, TB>(this FixExprI expr, IAlgebra<TI, TI, TB, TB> fold)
        => new Cata<TI, TB>(fold).Fold(expr);

    public static TB Fold<TI, TB>(this FixExprB expr, IAlgebra<TI, TI, TB, TB> fold)
        => new Cata<TI, TB>(fold).Fold(expr);
}

class Program
{
    static void TestExpr(FixExprB e)
    {
        Console.Write(e.Fold(new ReprAlgebra()));
        Console.Write(" = ");
        Console.Write(e.Fold(new EvalAlgebra()));
        Console.WriteLine();
    }

    static void TestExpr(FixExprI e)
    {
        Console.Write(e.Fold(new ReprAlgebra()));
        Console.Write(" = ");
        Console.Write(e.Fold(new EvalAlgebra()));
        Console.WriteLine();
    }

    public static int Main(params string[] args)
    {
        var op1 = Option.Some(1);
        var op2 = op1.Select(x => x + 2);
        Console.WriteLine(op1);
        Console.WriteLine(op2);

        var op3 = op2.SelectMany(v => Option.Some(v));
        Console.WriteLine(op3);
        var op4 = Option.None<int>();
        var op5 = op4.SelectMany(v => Option.Some(v));
        Console.WriteLine(op4);
        Console.WriteLine(op5);

        IAlgebra<FixExprI, FixExprI, FixExprB, FixExprB> f = new Factory();
        TestExpr(f.Add(f.Mul(f.LitInt(2), f.LitInt(10)), f.LitInt(22)));
        TestExpr(f.Gt(f.LitInt(2), f.LitInt(10)));
        TestExpr(f.And(f.LitBool(true), f.LitBool(false)));
        TestExpr(f.Select(f.LitBool(true), f.LitInt(2), f.LitInt(10)));
        TestExpr(f.Select(f.LitBool(false), f.LitInt(2), f.LitInt(10)));
        TestExpr(f.Select(f.Gt(f.LitInt(2), f.LitInt(10)), f.LitInt(2), f.LitInt(10)));
        TestExpr(f.Select(f.And(f.LitBool(true), f.LitBool(false)), f.LitInt(2), f.LitInt(10)));
        TestExpr(f.Select(f.Gt(f.LitInt(2), f.LitInt(10)), f.Add(f.LitInt(2), f.LitInt(10)),
            f.Mul(f.LitInt(2), f.LitInt(10))));
        TestExpr(f.LitInt(42));
        TestExpr(f.LitBool(true));
        TestExpr(f.Select(f.LitBool(false), f.LitInt(2), f.LitInt(10)));
        TestExpr(f.Select(f.Gt(f.LitInt(2), f.LitInt(10)), f.LitInt(2), f.LitInt(10)));
        TestExpr(f.Select(f.And(f.LitBool(true), f.LitBool(false)), f.LitInt(2), f.LitInt(10)));


        return 0;
    }
}
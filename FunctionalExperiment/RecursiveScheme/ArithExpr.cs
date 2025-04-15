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
    public IExprI<TIR, TBR> Select<TIR, TBR>(Func<TI, TIR> f, Func<TB, TBR> g);
}

sealed record class LiftedI<TI, TB>(IntLang.IExpr<TI> Expr) : IExprI<TI, TB>
{
    public TRI Apply<TRI, TRB>(IAlgebra<TI, TRI, TB, TRB> algebra)
        => Expr.Apply(algebra);

    public IExprI<TIR, TBR> Select<TIR, TBR>(Func<TI, TIR> f, Func<TB, TBR> g)
        => new LiftedI<TIR, TBR>(Expr.Select(f));
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
        => new LiftedB<TIR, TBR>(Expr.Select(g));
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

interface IFixI<TI, TB>
    where TI : IFixI<TI, TB>
    where TB : IFixB<TI, TB>
{
}

interface IFixB<TI, TB>
    where TI : IFixI<TI, TB>
    where TB : IFixB<TI, TB>
{
}

sealed record class FixExprI(IExprI<FixExprI, FixExprB> Expr)
    : IExprI<FixExprI, FixExprB>
    , IntLang.IFix<FixExprI>
{
    FixExprI IntLang.IFix<FixExprI>.Self() => this;

    public TRI Apply<TRI, TRB>(IAlgebra<FixExprI, TRI, FixExprB, TRB> algebra)
        => Expr.Apply(algebra);

    public IExprI<TIR, TBR> Select<TIR, TBR>(Func<FixExprI, TIR> f, Func<FixExprB, TBR> g)
        => Expr.Select(f, g);


    public static FixExprI Create(IntLang.IExpr<FixExprI> e)
        => e.Lift().Fix();
}

sealed record class FixExprB(IExprB<FixExprI, FixExprB> Expr)
    : IExprB<FixExprI, FixExprB>
    , BoolLang.IFix<FixExprB>
{
    public TRB Apply<TRI, TRB>(IAlgebra<FixExprI, TRI, FixExprB, TRB> algebra)
        => Expr.Apply(algebra);

    public IExprB<TIR, TBR> Select<TIR, TBR>(Func<FixExprI, TIR> f, Func<FixExprB, TBR> g)
        => Expr.Select(f, g);

    public FixExprB Self() => this;

    public static FixExprB Create(BoolLang.IExpr<FixExprB> e)
        => e.Lift().Fix();
}

sealed record class Cata<TI, TB>(IAlgebra<TI, TI, TB, TB> Folder)
{
    public TI Fold(FixExprI e)
        => e.Select(Fold, Fold).Apply(Folder);

    public TB Fold(FixExprB e)
        => e.Select(Fold, Fold).Apply(Folder);
}

sealed class Factory :
    IAlgebra<FixExprI, FixExprI, FixExprB, FixExprB>
  , IntLang.IFactory<FixExprI>
  , BoolLang.IFactory<FixExprB>
{
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
}

sealed class EvalAlgebra : IAlgebra<int, int, bool, bool>
{
    public int LitInt(int value) => value;

    public bool LitBool(bool value) => value;

    public bool And(bool a, bool b) => a && b;


    public bool Gt(int a, int b) => a > b;
    public int Add(int a, int b) => a + b;
    public int Mul(int a, int b) => a * b;

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

    public static IExprI<FixExprI, FixExprB> Lift(this IntLang.IExpr<FixExprI> e) =>
        new LiftedI<FixExprI, FixExprB>(e);

    public static IExprB<FixExprI, FixExprB> Lift(this BoolLang.IExpr<FixExprB> e) =>
        new LiftedB<FixExprI, FixExprB>(e);

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
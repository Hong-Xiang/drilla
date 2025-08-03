using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Expression;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;

namespace DualDrill.CLSL.Language;

public interface IJump<out TT, out TE>
{
    TT TargetRegion { get; }
    IReadOnlyList<TE> Arguments { get; }
}


public interface ITerminatorSemantic<in TX, in TT, in TE, out TO>
{
    TO ReturnVoid(TX context);
    TO ReturnExpr(TX context, TE expr);
    TO Br(TX context, TT target);
    TO BrIf(TX context, TE condition, TT trueTarget, TT falseTarget);
}

public interface ITerminator<TT, TE> : ISuccessor<TT>
{
    public TR Evaluate<TX, TR>(ITerminatorSemantic<TX, TT, TE, TR> semantic, TX context);


    public ITerminator<TTR, TER> Select<TTR, TER>(Func<TT, TTR> f, Func<TE, TER> g);
}

public abstract class Terminator
{
    public static class D
    {
        public sealed record class ReturnVoid<TT, TE> : ITerminator<TT, TE>
        {
            public TR Evaluate<TX, TR>(ITerminatorSemantic<TX, TT, TE, TR> semantic, TX context)
                => semantic.ReturnVoid(context);

            public TR Evaluate<TX, TR>(ISuccessorSemantic<TX, TT, TR> semantic, TX context)
                => semantic.Terminate(context);

            public ITerminator<TTR, TER> Select<TTR, TER>(Func<TT, TTR> f, Func<TE, TER> g)
                => new ReturnVoid<TTR, TER>();

            public override string ToString()
                => "return";
        }

        public sealed record class ReturnExpr<TT, TE>(TE Expr) : ITerminator<TT, TE>
        {
            public ITerminator<TR, TE> Map<TR>(ILabelMap<TT, TR> f)
                => new ReturnExpr<TR, TE>(Expr);

            public TR Evaluate<TX, TR>(ITerminatorSemantic<TX, TT, TE, TR> semantic, TX context)
                => semantic.ReturnExpr(context, Expr);
            public TR Evaluate<TX, TR>(ISuccessorSemantic<TX, TT, TR> semantic, TX context)
                => semantic.Terminate(context);

            public override string ToString()
                => $"return {Expr}";

            public ITerminator<TTR, TER> Select<TTR, TER>(Func<TT, TTR> f, Func<TE, TER> g)
                => new ReturnExpr<TTR, TER>(g(Expr));
        }

        public sealed record class Br<TT, TE>(TT Target) : ITerminator<TT, TE>
        {
            public TR Evaluate<TX, TR>(ITerminatorSemantic<TX, TT, TE, TR> semantic, TX context)
                => semantic.Br(context, Target);
            public TR Evaluate<TX, TR>(ISuccessorSemantic<TX, TT, TR> semantic, TX context)
                => semantic.Unconditional(context, Target);


            public ITerminator<TTR, TER> Select<TTR, TER>(Func<TT, TTR> f, Func<TE, TER> g)
                => new Br<TTR, TER>(f(Target));

            public override string ToString()
                => $"br {Target}";
        }

        public sealed record class BrIf<TT, TE>(TE Condition, TT TrueTarget, TT FalseTarget) : ITerminator<TT, TE>
        {
            public TR Evaluate<TX, TR>(ITerminatorSemantic<TX, TT, TE, TR> semantic, TX context)
                => semantic.BrIf(context, Condition, TrueTarget, FalseTarget);
            public TR Evaluate<TX, TR>(ISuccessorSemantic<TX, TT, TR> semantic, TX context)
                => semantic.Conditional(context, TrueTarget, FalseTarget);


            public ITerminator<TTR, TER> Select<TTR, TER>(Func<TT, TTR> f, Func<TE, TER> g)
                => new BrIf<TTR, TER>(g(Condition), f(TrueTarget), f(FalseTarget));

            public override string ToString()
                => $"br_if {Condition} {TrueTarget} {FalseTarget}";
        }
    }

    public static class B
    {
        public static ITerminator<TT, TE> ReturnVoid<TT, TE>()
            => new D.ReturnVoid<TT, TE>();

        public static ITerminator<TT, TE> ReturnExpr<TT, TE>(TE expr)
            => new D.ReturnExpr<TT, TE>(expr);

        public static ITerminator<TT, TE> Br<TT, TE>(TT target)
            => new D.Br<TT, TE>(target);

        public static ITerminator<TT, TE> BrIf<TT, TE>(TE condition, TT trueTarget, TT falseTarget)
            => new D.BrIf<TT, TE>(condition, trueTarget, falseTarget);
    }

    sealed class FactorySemantic<TT, TE> : ITerminatorSemantic<Unit, TT, TE, ITerminator<TT, TE>>
    {
        public ITerminator<TT, TE> Br(Unit context, TT target)
            => B.Br<TT, TE>(target);

        public ITerminator<TT, TE> BrIf(Unit context, TE condition, TT trueTarget, TT falseTarget)
            => B.BrIf(condition, trueTarget, falseTarget);

        public ITerminator<TT, TE> ReturnExpr(Unit context, TE expr)
            => B.ReturnExpr<TT, TE>(expr);

        public ITerminator<TT, TE> ReturnVoid(Unit context)
            => B.ReturnVoid<TT, TE>();
    }
    public static ITerminatorSemantic<Unit, TT, TE, ITerminator<TT, TE>> Factory<TT, TE>() => new FactorySemantic<TT, TE>();
}

public static class TerminatorExtension
{
    sealed class ToSuccessorSemantic<TE> : ITerminatorSemantic<Unit, Label, TE, ISuccessor>
    {
        public ISuccessor Br(Unit context, Label target)
            => new UnconditionalSuccessor(target);
        public ISuccessor BrIf(Unit context, TE condition, Label trueTarget, Label falseTarget)
            => new ConditionalSuccessor(trueTarget, falseTarget);
        public ISuccessor ReturnExpr(Unit context, TE expr)
            => new TerminateSuccessor();
        public ISuccessor ReturnVoid(Unit context)
            => new TerminateSuccessor();
    }
    public static ISuccessor ToSuccessor<TE>(this ITerminator<Label, TE> t)
        => t.Evaluate(new ToSuccessorSemantic<TE>(), default);

    public static ITerminator<TR, TE> Map<TT, TE, TR>(this ITerminator<TT, TE> t, ILabelMap<TT, TR> f)
        => t.Select(f.MapLabel, static x => x);

    public static ITerminator<TT, TR> Map<TT, TE, TR>(this ITerminator<TT, TE> t, IExprTransform<TE, TR> f)
        => t.Select(static x => x, f.MapExpr);

}

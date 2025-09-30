using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language;

public interface ITerminatorSemantic<in TT, in TE, out TO>
{
    TO ReturnVoid();
    TO ReturnExpr(TE expr);
    TO Br(TT target);
    TO BrIf(TE condition, TT trueTarget, TT falseTarget);
}

public interface ITerminator<TT, TE> : ISuccessor<TT>
{
    public TR Evaluate<TR>(ITerminatorSemantic<TT, TE, TR> semantic);
    public ITerminator<TTR, TER> Select<TTR, TER>(Func<TT, TTR> f, Func<TE, TER> g);
}

public abstract class Terminator
{
    public static ITerminatorSemantic<TT, TE, ITerminator<TT, TE>> Factory<TT, TE>() => new FactorySemantic<TT, TE>();

    public static class D
    {
        public sealed record class ReturnVoid<TT, TE> : ITerminator<TT, TE>
        {
            public TR Evaluate<TR>(ITerminatorSemantic<TT, TE, TR> semantic) => semantic.ReturnVoid();

            public TR Evaluate<TX, TR>(ISuccessorSemantic<TX, TT, TR> semantic, TX context) =>
                semantic.Terminate(context);

            public ITerminator<TTR, TER> Select<TTR, TER>(Func<TT, TTR> f, Func<TE, TER> g) =>
                new ReturnVoid<TTR, TER>();

            public override string ToString() => "return";
        }

        public sealed record class ReturnExpr<TT, TE>(TE Expr) : ITerminator<TT, TE>
        {
            public TR Evaluate<TR>(ITerminatorSemantic<TT, TE, TR> semantic) => semantic.ReturnExpr(Expr);

            public TR Evaluate<TX, TR>(ISuccessorSemantic<TX, TT, TR> semantic, TX context) =>
                semantic.Terminate(context);

            public ITerminator<TTR, TER> Select<TTR, TER>(Func<TT, TTR> f, Func<TE, TER> g) =>
                new ReturnExpr<TTR, TER>(g(Expr));

            public ITerminator<TR, TE> Map<TR>(ILabelMap<TT, TR> f) => new ReturnExpr<TR, TE>(Expr);

            public override string ToString() => $"return {Expr}";
        }

        public sealed record class Br<TT, TE>(TT Target) : ITerminator<TT, TE>
        {
            public TR Evaluate<TR>(ITerminatorSemantic<TT, TE, TR> semantic) => semantic.Br(Target);

            public TR Evaluate<TX, TR>(ISuccessorSemantic<TX, TT, TR> semantic, TX context) =>
                semantic.Unconditional(context, Target);


            public ITerminator<TTR, TER> Select<TTR, TER>(Func<TT, TTR> f, Func<TE, TER> g) =>
                new Br<TTR, TER>(f(Target));

            public override string ToString() => $"br {Target}";
        }

        public sealed record class BrIf<TT, TE>(TE Condition, TT TrueTarget, TT FalseTarget) : ITerminator<TT, TE>
        {
            public TR Evaluate<TR>(ITerminatorSemantic<TT, TE, TR> semantic) =>
                semantic.BrIf(Condition, TrueTarget, FalseTarget);

            public TR Evaluate<TX, TR>(ISuccessorSemantic<TX, TT, TR> semantic, TX context) =>
                semantic.Conditional(context, TrueTarget, FalseTarget);


            public ITerminator<TTR, TER> Select<TTR, TER>(Func<TT, TTR> f, Func<TE, TER> g) =>
                new BrIf<TTR, TER>(g(Condition), f(TrueTarget), f(FalseTarget));

            public override string ToString() => $"br_if {Condition} {TrueTarget} {FalseTarget}";
        }
    }

    public static class B
    {
        public static ITerminator<TT, TE> ReturnVoid<TT, TE>() => new D.ReturnVoid<TT, TE>();

        public static ITerminator<TT, TE> ReturnExpr<TT, TE>(TE expr) => new D.ReturnExpr<TT, TE>(expr);

        public static ITerminator<TT, TE> Br<TT, TE>(TT target) => new D.Br<TT, TE>(target);

        public static ITerminator<TT, TE> BrIf<TT, TE>(TE condition, TT trueTarget, TT falseTarget) =>
            new D.BrIf<TT, TE>(condition, trueTarget, falseTarget);
    }

    private sealed class FactorySemantic<TT, TE> : ITerminatorSemantic<TT, TE, ITerminator<TT, TE>>
    {
        public ITerminator<TT, TE> Br(TT target) => B.Br<TT, TE>(target);

        public ITerminator<TT, TE> BrIf(TE condition, TT trueTarget, TT falseTarget) =>
            B.BrIf(condition, trueTarget, falseTarget);

        public ITerminator<TT, TE> ReturnExpr(TE expr) => B.ReturnExpr<TT, TE>(expr);

        public ITerminator<TT, TE> ReturnVoid() => B.ReturnVoid<TT, TE>();
    }
}

public static class TerminatorExtension
{
    public static ISuccessor ToSuccessor<TE>(this ITerminator<Label, TE> t) =>
        t.Evaluate(new ToSuccessorSemantic<TE>());

    public static ISuccessor ToSuccessor<TE>(this ITerminator<RegionJump, TE> t)
    {
        return t.Select(l => l.Label, e => e).Evaluate(new ToSuccessorSemantic<TE>());
    }

    public static ITerminator<TR, TE> Map<TT, TE, TR>(this ITerminator<TT, TE> t, ILabelMap<TT, TR> f)
    {
        return t.Select(f.MapLabel, static x => x);
    }

    private sealed class ToSuccessorSemantic<TE> : ITerminatorSemantic<Label, TE, ISuccessor>
    {
        public ISuccessor Br(Label target) => new UnconditionalSuccessor(target);

        public ISuccessor BrIf(TE condition, Label trueTarget, Label falseTarget) =>
            new ConditionalSuccessor(trueTarget, falseTarget);

        public ISuccessor ReturnExpr(TE expr) => new TerminateSuccessor();

        public ISuccessor ReturnVoid() => new TerminateSuccessor();
    }
}
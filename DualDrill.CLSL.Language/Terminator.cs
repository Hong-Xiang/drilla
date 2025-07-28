namespace DualDrill.CLSL.Language;

public interface IJump<out TT, out TE>
{
    TT TargetRegion { get; }
    IReadOnlyList<TE> Arguments { get; }
}


public interface ITerminatorSemantic<in TT, in TE, out TO>
{
    TO ReturnVoid();
    TO ReturnExpr(TE expr);
    TO Br(TT target);
    TO BrIf(TE condition, TT trueTarget, TT falseTarget);
}

public interface ITerminator<TT, TE>
{
    public TR Evaluate<TR>(ITerminatorSemantic<TT, TE, TR> semantic);
}

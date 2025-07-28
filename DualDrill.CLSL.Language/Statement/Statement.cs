namespace DualDrill.CLSL.Language.Statement;

public interface IStatementSemantic<in TV, in TM, in TE, out TO>
{
    TO Bind(TV value, TE expr);
    TO Load(TV value, TM memory);
    TO Store(TM memory, TV value);
}

public interface IStatement<out TV, out TM, out TE>
{
    public TR Evalute<TR>(IStatementSemantic<TV, TM, TE, TR> semantic);
    public IStatement<TVR, TMR, TER> Select<TVR, TMR, TER>(Func<TV, TVR> fv, Func<TM, TMR> fm, Func<TE, TER> fe);
}
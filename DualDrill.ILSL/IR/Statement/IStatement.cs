namespace DualDrill.ILSL.IR.Statement;

public interface IStatement { }

public interface IStatementVisitor<T>
{
    T VisitReturn(ReturnStatement stmt);
    T VisitVariableOrValue(VariableOrValueStatement stmt);
    T VisitCompound(CompoundStatement stmt);
}

public static class StatementExtension
{
    public static T AcceptVisitor<T>(this IStatement stmt, IStatementVisitor<T> visitor)
    {
        return stmt switch
        {
            ReturnStatement s => visitor.VisitReturn(s),
            VariableOrValueStatement s => visitor.VisitVariableOrValue(s),
            CompoundStatement s => visitor.VisitCompound(s),
            _ => throw new NotSupportedException($"visit {nameof(IStatement)} does not support {stmt}")
        };
    }
}




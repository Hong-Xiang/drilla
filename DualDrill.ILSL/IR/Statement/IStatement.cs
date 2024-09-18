using System.Text.Json.Serialization;

namespace DualDrill.ILSL.IR.Statement;

[JsonDerivedType(typeof(ReturnStatement), nameof(ReturnStatement))]
[JsonDerivedType(typeof(VariableOrValueStatement), nameof(VariableOrValueStatement))]
[JsonDerivedType(typeof(CompoundStatement), nameof(CompoundStatement))]
[JsonDerivedType(typeof(IfStatement), nameof(IfStatement))]
[JsonDerivedType(typeof(SimpleAssignmentStatement), nameof(SimpleAssignmentStatement))]
[JsonDerivedType(typeof(PhonyAssignmentStatement), nameof(PhonyAssignmentStatement))]
[JsonDerivedType(typeof(WhileStatement), nameof(WhileStatement))]
[JsonDerivedType(typeof(BreakStatement), nameof(BreakStatement))]
[JsonDerivedType(typeof(ForStatement), nameof(ForStatement))]
public interface IStatement : INode { }

public interface IStatementVisitor<T>
{
    T VisitReturn(ReturnStatement stmt);
    T VisitVariableOrValue(VariableOrValueStatement stmt);
    T VisitCompound(CompoundStatement stmt);
    T VisitIf(IfStatement stmt);
    T VisitWhile(WhileStatement stmt);
    T VisitBreak(BreakStatement stmt);
    T VisitFor(ForStatement stmt);
    T VisitSimpleAssignment(SimpleAssignmentStatement stmt);
    T VisitPhonyAssignment(PhonyAssignmentStatement stmt);

    T AppendSemicolon(T t);
}

public static class StatementExtension
{
    public static T AcceptVisitor<T>(this IStatement stmt, IStatementVisitor<T> visitor)
    {
        return stmt switch
        {
            ReturnStatement s => visitor.AppendSemicolon(visitor.VisitReturn(s)),
            VariableOrValueStatement s => visitor.AppendSemicolon(visitor.VisitVariableOrValue(s)),
            CompoundStatement s => visitor.VisitCompound(s),
            IfStatement s => visitor.VisitIf(s),
            WhileStatement s => visitor.VisitWhile(s),
            BreakStatement s => visitor.AppendSemicolon(visitor.VisitBreak(s)),
            ForStatement s => visitor.VisitFor(s),
            SimpleAssignmentStatement s => visitor.AppendSemicolon(visitor.VisitSimpleAssignment(s)),
            PhonyAssignmentStatement s => visitor.AppendSemicolon(visitor.VisitPhonyAssignment(s)),
            _ => throw new NotSupportedException($"visit {nameof(IStatement)} does not support {stmt}")
        };
    }
}




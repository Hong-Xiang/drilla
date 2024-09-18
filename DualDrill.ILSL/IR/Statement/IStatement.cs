using System.Text.Json.Serialization;

namespace DualDrill.ILSL.IR.Statement;

[JsonDerivedType(typeof(ReturnStatement), nameof(ReturnStatement))]
[JsonDerivedType(typeof(VariableOrValueStatement), nameof(VariableOrValueStatement))]
[JsonDerivedType(typeof(CompoundStatement), nameof(CompoundStatement))]
[JsonDerivedType(typeof(IfStatement), nameof(IfStatement))]
[JsonDerivedType(typeof(SimpleAssignmentStatement), nameof(SimpleAssignmentStatement))]
[JsonDerivedType(typeof(PhonyAssignmentStatement), nameof(PhonyAssignmentStatement))]
public interface IStatement : INode { }

public interface IStatementVisitor<T>
{
    T VisitReturn(ReturnStatement stmt);
    T VisitVariableOrValue(VariableOrValueStatement stmt);
    T VisitCompound(CompoundStatement stmt);
    T VisitIf(IfStatement stmt);
    T VisitWhile(WhileStatement stmt);
    T VisitBreak(BreakStatement stmt);
    T VisitSimpleAssignment(SimpleAssignmentStatement stmt);
    T VisitPhonyAssignment(PhonyAssignmentStatement stmt);
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
            IfStatement s => visitor.VisitIf(s),
            WhileStatement s => visitor.VisitWhile(s),
            BreakStatement s => visitor.VisitBreak(s),
            SimpleAssignmentStatement s => visitor.VisitSimpleAssignment(s),
            PhonyAssignmentStatement s => visitor.VisitPhonyAssignment(s),
            _ => throw new NotSupportedException($"visit {nameof(IStatement)} does not support {stmt}")
        };
    }
}




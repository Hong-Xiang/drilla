using System.Text.Json.Serialization;

namespace DualDrill.ILSL.IR.Statement;

[JsonDerivedType(typeof(SimpleAssignmentStatement), nameof(SimpleAssignmentStatement))]
[JsonDerivedType(typeof(PhonyAssignmentStatement), nameof(PhonyAssignmentStatement))]
[JsonDerivedType(typeof(IncrementStatement), nameof(IncrementStatement))]
[JsonDerivedType(typeof(DecrementStatement), nameof(DecrementStatement))]
public interface IForUpdate
{
}

public static class ForUpdateExtension
{
    public static T AcceptVisitor<T>(this IForUpdate stmt, IStatementVisitor<T> visitor)
    {
        return stmt switch
        {
            SimpleAssignmentStatement s => visitor.VisitSimpleAssignment(s),
            PhonyAssignmentStatement s => visitor.VisitPhonyAssignment(s),
            IncrementStatement s => visitor.VisitIncrement(s),
            DecrementStatement s => visitor.VisitDecrement(s),
            _ => throw new NotSupportedException($"visit {nameof(IForUpdate)} does not support {stmt}")
        };
    }
}

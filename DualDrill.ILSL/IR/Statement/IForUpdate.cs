using System.Text.Json.Serialization;

namespace DualDrill.ILSL.IR.Statement;

[JsonDerivedType(typeof(SimpleAssignmentStatement), nameof(SimpleAssignmentStatement))]
[JsonDerivedType(typeof(PhonyAssignmentStatement), nameof(PhonyAssignmentStatement))]
public interface IForUpdate : IStatement
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
            _ => throw new NotSupportedException($"visit {nameof(IForUpdate)} does not support {stmt}")
        };
    }
}

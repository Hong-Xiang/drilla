using System.Text.Json.Serialization;

namespace DualDrill.ILSL.IR.Statement;

[JsonDerivedType(typeof(VariableOrValueStatement), nameof(VariableOrValueStatement))]
[JsonDerivedType(typeof(SimpleAssignmentStatement), nameof(SimpleAssignmentStatement))]
[JsonDerivedType(typeof(PhonyAssignmentStatement), nameof(PhonyAssignmentStatement))]
public interface IForInit
{
}

public static class ForInitExtension
{
    public static T AcceptVisitor<T>(this IForInit stmt, IStatementVisitor<T> visitor)
    {
        return stmt switch
        {
            VariableOrValueStatement s => visitor.VisitVariableOrValue(s),
            SimpleAssignmentStatement s => visitor.VisitSimpleAssignment(s),
            PhonyAssignmentStatement s => visitor.VisitPhonyAssignment(s),
            _ => throw new NotSupportedException($"visit {nameof(IForInit)} does not support {stmt}")
        };
    }
}

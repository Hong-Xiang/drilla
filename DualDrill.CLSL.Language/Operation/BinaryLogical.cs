using System.Text.Json.Serialization;

namespace DualDrill.CLSL.Language.Operation;

public static class BinaryLogical
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OpKind
    {
        and,
        or,
        xor,
    }

    public interface IOp<TSelf> : IOpKind<TSelf, OpKind>, IBinaryOp<TSelf>
        where TSelf : IOp<TSelf>
    {
        BinaryArithmetic.IBitwiseLogicalOp BitwiseOp { get; }
    }
}

public sealed class LogicalAnd : BinaryLogical.IOp<LogicalAnd>, IIntegerOp<LogicalAnd>
{
    public static BinaryLogical.OpKind Kind => BinaryLogical.OpKind.and;

    public static LogicalAnd Instance { get; } = new();

    public BinaryArithmetic.IBitwiseLogicalOp BitwiseOp => BinaryArithmetic.BitwiseAnd.Instance;

}
public sealed class LogicalOr : BinaryLogical.IOp<LogicalOr>, IIntegerOp<LogicalOr>
{
    public static BinaryLogical.OpKind Kind => BinaryLogical.OpKind.or;
    public static LogicalOr Instance { get; } = new();
    public BinaryArithmetic.IBitwiseLogicalOp BitwiseOp => BinaryArithmetic.BitwiseOr.Instance;
}
public sealed class LogicalXor : BinaryLogical.IOp<LogicalXor>, IIntegerOp<LogicalXor>
{
    public static BinaryLogical.OpKind Kind => BinaryLogical.OpKind.xor;
    public static LogicalXor Instance { get; } = new();
    public BinaryArithmetic.IBitwiseLogicalOp BitwiseOp => BinaryArithmetic.BitwiseXor.Instance;
}

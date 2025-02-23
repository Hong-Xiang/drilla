using System.Text.Json.Serialization;

namespace DualDrill.CLSL.Language.Operation;

public static class BinaryLogical
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OpKind
    {
        and,
        or,
        xor
    }

    public interface IOp
    {
    }

    public interface IOp<TSelf> : IOp, IBinaryOp<TSelf>
        where TSelf : IOp<TSelf>
    {
    }

    public interface IWithBitwiseOp<TSelf> : IOp<TSelf>, IOpKind<TSelf, OpKind>
        where TSelf : IWithBitwiseOp<TSelf>
    {
        BinaryArithmetic.IBitwiseLogicalOp BitwiseOp { get; }
    }
}

public sealed class LogicalAnd : BinaryLogical.IWithBitwiseOp<LogicalAnd>, IIntegerOp<LogicalAnd>, ISymbolOp<LogicalAnd>
{
    public static BinaryLogical.OpKind Kind => BinaryLogical.OpKind.and;

    public static LogicalAnd Instance { get; } = new();

    public BinaryArithmetic.IBitwiseLogicalOp BitwiseOp => BinaryArithmetic.BitwiseAnd.Instance;
    public string Symbol => "&&";
}

public sealed class LogicalOr : BinaryLogical.IWithBitwiseOp<LogicalOr>, IIntegerOp<LogicalOr>, ISymbolOp<LogicalOr>
{
    public static BinaryLogical.OpKind Kind => BinaryLogical.OpKind.or;
    public static LogicalOr Instance { get; } = new();
    public BinaryArithmetic.IBitwiseLogicalOp BitwiseOp => BinaryArithmetic.BitwiseOr.Instance;
    public string Symbol => "||";
}

public sealed class LogicalXor : BinaryLogical.IWithBitwiseOp<LogicalXor>, IIntegerOp<LogicalXor>, ISymbolOp<LogicalXor>
{
    public static BinaryLogical.OpKind Kind => BinaryLogical.OpKind.xor;
    public static LogicalXor Instance { get; } = new();
    public BinaryArithmetic.IBitwiseLogicalOp BitwiseOp => BinaryArithmetic.BitwiseXor.Instance;
    public string Symbol => "^";
}
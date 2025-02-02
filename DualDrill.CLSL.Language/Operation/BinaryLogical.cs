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
    }
}

public sealed class OpAnd : BinaryLogical.IOp<OpAnd>, IIntegerOp<OpAnd>
{
    public static BinaryLogical.OpKind Kind => BinaryLogical.OpKind.and;
}
public sealed class OpOr : BinaryLogical.IOp<OpOr>, IIntegerOp<OpOr>
{
    public static BinaryLogical.OpKind Kind => BinaryLogical.OpKind.or;
}
public sealed class OpXor : BinaryLogical.IOp<OpXor>, IIntegerOp<OpXor>
{
    public static BinaryLogical.OpKind Kind => BinaryLogical.OpKind.xor;
}

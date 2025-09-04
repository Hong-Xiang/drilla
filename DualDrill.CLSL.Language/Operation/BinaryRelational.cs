using System.Text.Json.Serialization;

namespace DualDrill.CLSL.Language.Operation;

public static class BinaryRelational
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OpKind
    {
        lt,
        gt,
        le,
        ge,
        eq,
        ne
    }

    public interface IOp<TSelf> : IOpKind<TSelf, OpKind>, IBinaryOp<TSelf>
        where TSelf : IOp<TSelf>
    {
    }

    public sealed class Lt : IOp<Lt>, IFloatOp<Lt>, ISignedIntegerOp<Lt>, ISymbolOp<Lt>
    {
        public static OpKind Kind => OpKind.lt;

        public static Lt Instance { get; } = new();

        public string Symbol => "<";
    }

    public sealed class Gt : IOp<Gt>, IFloatOp<Gt>, ISignedIntegerOp<Gt>, ISymbolOp<Gt>
    {
        public static OpKind Kind => OpKind.gt;
        public static Gt Instance { get; } = new();

        public string Symbol => ">";
    }

    public sealed class Le : IOp<Le>, IFloatOp<Le>, ISignedIntegerOp<Le>, ISymbolOp<Le>
    {
        public static OpKind Kind => OpKind.le;
        public static Le Instance { get; } = new();

        public string Symbol => "<=";
    }

    public sealed class Ge : IOp<Ge>, IFloatOp<Ge>, ISignedIntegerOp<Ge>, ISymbolOp<Ge>
    {
        public static OpKind Kind => OpKind.ge;
        public static Ge Instance { get; } = new();

        public string Symbol => ">=";
    }

    public sealed class Eq : IOp<Eq>, IFloatOp<Eq>, IIntegerOp<Eq>, ISymbolOp<Eq>, BinaryLogical.IOp<Eq>
    {
        public static OpKind Kind => OpKind.eq;
        public static Eq Instance { get; } = new();

        public string Symbol => "==";
        public string Name => "eq";
    }

    public sealed class Ne : IOp<Ne>, IFloatOp<Ne>, IIntegerOp<Ne>, ISymbolOp<Ne>, BinaryLogical.IOp<Ne>
    {
        public static OpKind Kind => OpKind.ne;
        public static Ne Instance { get; } = new();
        public string Symbol => "!=";
        public string Name => "ne";
    }
}
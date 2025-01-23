using DotNext.Patterns;
using System.Text.Json.Serialization;

namespace DualDrill.CLSL.Language.Operation;

public static class BinaryRelation
{
    public interface IOp
    {
        public Op Value { get; }
    }

    public interface IOp<TOp> : IOp, ISingleton<TOp>
        where TOp : class, IOp<TOp>
    {
    }


    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Op
    {
        lt,
        gt,
        le,
        ge,
        eq,
        ne
    }

    public sealed class Lt : IOp<Lt>, IFloatOp, ISignednessIntegerOp
    {
        public static Lt Instance { get; } = new();
        public Op Value => Op.lt;
    }

    public sealed class Gt : IOp<Gt>, IFloatOp, ISignednessIntegerOp
    {
        public static Gt Instance { get; } = new();
        public Op Value => Op.gt;

    }

    public sealed class Le : IOp<Le>, IFloatOp, ISignednessIntegerOp
    {
        public static Le Instance { get; } = new();
        public Op Value => Op.le;
    }

    public sealed class Ge : IOp<Ge>, IFloatOp, ISignednessIntegerOp
    {
        public static Ge Instance { get; } = new();
        public Op Value => Op.ge;
    }

    public sealed class Eq : IOp<Eq>, IFloatOp, ISignednessIntegerOp
    {
        public static Eq Instance { get; } = new();
        public Op Value => Op.eq;
    }

    public sealed class Ne : IOp<Ne>, IFloatOp, ISignednessIntegerOp
    {
        public static Ne Instance { get; } = new();
        public Op Value => Op.ne;
    }
}


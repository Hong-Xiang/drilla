using DotNext.Patterns;
using System.Runtime.CompilerServices;
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
        public Op Value => Op.lt;
    }
}

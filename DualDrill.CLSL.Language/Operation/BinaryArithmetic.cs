using DotNext.Patterns;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace DualDrill.CLSL.Language.Operation;

public static class BinaryArithmetic
{
    public interface IOp
    {
        public Op Value { get; }
        public string Symbol { get; }

    }

    public interface IOp<TOp> : IOp, ISingleton<TOp>
        where TOp : class, IOp<TOp>
    {
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Op
    {
        add,
        sub,
        mul,
        div,
        rem,
        min,
        max,
        copysign
    }

    public sealed class Add : IOp<Add>
    {
        public static Add Instance { get; } = new Add();
        public Op Value => Op.add;
        public string Symbol => "+";
    }
    public sealed class Sub : IOp<Sub>
    {
        public static Sub Instance { get; } = new Sub();

        public Op Value => Op.sub;
        public string Symbol => "-";
    }
    public sealed class Mul : IOp<Mul>
    {
        public static Mul Instance { get; } = new Mul();
        public Op Value => Op.mul;
        public string Symbol => "*";
    }
    public sealed class Div : IOp<Div>
    {
        public static Div Instance { get; } = new Div();
        public Op Value => Op.div;
        public string Symbol => "/";
    }
    public sealed class Rem : IOp<Rem>
    {
        public static Rem Instance { get; } = new Rem();
        public Op Value => Op.rem;
        public string Symbol => "%";
    }

    public sealed class Min : IOp<Min>
    {
        public static Min Instance => throw new NotImplementedException();

        public Op Value => throw new NotImplementedException();

        public string Symbol => throw new NotImplementedException();
    }
    public sealed class Max : IOp<Max>
    {
        public static Max Instance => throw new NotImplementedException();

        public Op Value => throw new NotImplementedException();

        public string Symbol => throw new NotImplementedException();
    }

    public static IOp GetInstance(Op op)
    {
        return op switch
        {
            Op.add => Add.Instance,
            Op.sub => Sub.Instance,
            Op.mul => Mul.Instance,
            Op.div => Div.Instance,
            Op.rem => Rem.Instance,
            _ => throw new InvalidEnumArgumentException(nameof(op), (int)op, typeof(Op))
        };
    }
}

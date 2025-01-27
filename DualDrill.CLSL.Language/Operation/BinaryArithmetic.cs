using DotNext.Patterns;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace DualDrill.CLSL.Language.Operation;

public static class BinaryArithmetic
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OpKind
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

    public interface IOp
    {
        public interface IVisitor<TResult>
        {
            TResult Visit<TOp>(TOp op) where TOp : IOp<TOp>;
        }

        TResult Accept<TVisitor, TResult>(TVisitor visitor) where TVisitor : IVisitor<TResult>;
    }

    public interface IOp<TSelf> : IOp, IOpKind<TSelf, OpKind>
        where TSelf : IOp<TSelf>
    {
    }

    public sealed class Add : IOp<Add>, ISymbolOp<Add>, ISingleton<Add>, IFloatOp<Add>, IIntegerOp<Add>
    {
        public static OpKind Kind => OpKind.add;
        public static string Symbol => "+";

        public static Add Instance { get; } = new();

        public TResult Accept<TVisitor, TResult>(TVisitor visitor) where TVisitor : IOp.IVisitor<TResult>
            => visitor.Visit(this);
    }

    public sealed class Sub : IOp<Sub>, ISymbolOp<Sub>, ISingleton<Sub>, IFloatOp<Sub>, IIntegerOp<Sub>
    {
        public static OpKind Kind => OpKind.sub;
        public static string Symbol => "-";

        public static Sub Instance { get; } = new();

        public TResult Accept<TVisitor, TResult>(TVisitor visitor) where TVisitor : IOp.IVisitor<TResult>
            => visitor.Visit(this);
    }


    public sealed class Mul : IOp<Mul>, ISymbolOp<Mul>, ISingleton<Mul>, IFloatOp<Mul>, IIntegerOp<Mul>
    {
        public static OpKind Kind => OpKind.mul;
        public static string Symbol => "*";

        public static Mul Instance { get; } = new();

        public TResult Accept<TVisitor, TResult>(TVisitor visitor) where TVisitor : IOp.IVisitor<TResult>
            => visitor.Visit(this);
    }


    public sealed class Div : IOp<Div>, ISymbolOp<Div>, ISingleton<Div>, IFloatOp<Div>, ISignedIntegerOp<Div>
    {
        public static OpKind Kind => OpKind.div;
        public static string Symbol => "/";

        public static Div Instance { get; } = new();

        public TResult Accept<TVisitor, TResult>(TVisitor visitor) where TVisitor : IOp.IVisitor<TResult>
            => visitor.Visit(this);
    }


    public sealed class Rem : IOp<Rem>, ISymbolOp<Rem>, ISingleton<Rem>, IFloatOp<Div>, ISignedIntegerOp<Div>
    {
        public static OpKind Kind => OpKind.rem;
        public static string Symbol => "%";

        public static Rem Instance { get; } = new();

        public TResult Accept<TVisitor, TResult>(TVisitor visitor) where TVisitor : IOp.IVisitor<TResult>
            => visitor.Visit(this);
    }

    public sealed class Min : IOp<Min>, ISingleton<Min>, IFloatOp<Min>
    {
        public static OpKind Kind => OpKind.min;

        public static Min Instance { get; } = new();

        public TResult Accept<TVisitor, TResult>(TVisitor visitor) where TVisitor : IOp.IVisitor<TResult>
            => visitor.Visit(this);
    }

    public sealed class Max : IOp<Max>, ISingleton<Max>, IFloatOp<Min>
    {
        public static OpKind Kind => OpKind.max;

        public static Max Instance { get; } = new();

        public TResult Accept<TVisitor, TResult>(TVisitor visitor) where TVisitor : IOp.IVisitor<TResult>
            => visitor.Visit(this);
    }

    public static IOp GetInstance(OpKind op)
    {
        return op switch
        {
            OpKind.add => Add.Instance,
            OpKind.sub => Sub.Instance,
            OpKind.mul => Mul.Instance,
            OpKind.div => Div.Instance,
            OpKind.rem => Rem.Instance,
            _ => throw new InvalidEnumArgumentException(nameof(op), (int)op, typeof(OpKind))
        };
    }
}

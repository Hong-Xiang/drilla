using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
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
        copysign,
        and,
        or,
        xor
    }

    public interface IOp
    {
        public interface IVisitor<TResult>
        {
            TResult Visit<TOp>(TOp op) where TOp : IOp<TOp>;
        }

        TResult Accept<TVisitor, TResult>(TVisitor visitor) where TVisitor : IVisitor<TResult>;

        INumericBinaryOperation GetNumericBinaryOperation(INumericType t);
    }

    public interface IOp<TSelf> : IOp, IOpKind<TSelf, OpKind>, ISingleton<TSelf>, IBinaryOp<TSelf>
        where TSelf : IOp<TSelf>
    {

        TResult IOp.Accept<TVisitor, TResult>(TVisitor visitor)
            => visitor.Visit(TSelf.Instance);


        INumericBinaryOperation IOp.GetNumericBinaryOperation(INumericType t)
        {
            return t.GetBinaryOperation<TSelf>();
        }
    }

    public sealed class Add : IOp<Add>, ISymbolOp<Add>, IFloatOp<Add>, IIntegerOp<Add>
    {
        public static Add Instance { get; } = new();

        public static OpKind Kind => OpKind.add;
        public static string Symbol => "+";
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
            OpKind.and => BitwiseAnd.Instance,
            OpKind.or => BitwiseOr.Instance,
            OpKind.xor => BitwiseXor.Instance,
            _ => throw new InvalidEnumArgumentException(nameof(op), (int)op, typeof(OpKind))
        };
    }

    public interface IBitwiseLogicalOp : IOp
    {
    }

    public sealed class BitwiseAnd
        : IOp<BitwiseAnd>
        , IIntegerOp<BitwiseAnd>
        , ISymbolOp<BitwiseAnd>
        , IBitwiseLogicalOp
    {
        public static OpKind Kind => OpKind.and;
        public static BitwiseAnd Instance { get; } = new();

        public static string Symbol => "&";

    }

    public sealed class BitwiseOr
        : IOp<BitwiseOr>
        , IIntegerOp<BitwiseOr>
        , ISymbolOp<BitwiseOr>
        , IBitwiseLogicalOp
    {
        public static OpKind Kind => OpKind.or;
        public static BitwiseOr Instance { get; } = new();
        public static string Symbol => "|";
    }

    public sealed class BitwiseXor
        : IOp<BitwiseXor>
        , IIntegerOp<BitwiseXor>
        , ISymbolOp<BitwiseAnd>
        , IBitwiseLogicalOp
    {
        public static OpKind Kind => OpKind.xor;
        public static BitwiseXor Instance { get; } = new();
        public static string Symbol => "^";
    }
}

﻿using System.Text.Json.Serialization;

namespace DualDrill.CLSL.Language.Operation;

public static class BinaryRelation
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

    public sealed class Lt : IOp<Lt>, IFloatOp<Lt>, ISignedIntegerOp<Lt>
    {
        public static OpKind Kind => OpKind.lt;

        public static Lt Instance { get; } = new();
    }

    public sealed class Gt : IOp<Gt>, IFloatOp<Gt>, ISignedIntegerOp<Gt>
    {
        public static OpKind Kind => OpKind.gt;
        public static Gt Instance { get; } = new();

    }

    public sealed class Le : IOp<Le>, IFloatOp<Le>, ISignedIntegerOp<Le>
    {
        public static OpKind Kind => OpKind.le;
        public static Le Instance { get; } = new();
    }

    public sealed class Ge : IOp<Ge>, IFloatOp<Ge>, ISignedIntegerOp<Ge>
    {
        public static OpKind Kind => OpKind.ge;
        public static Ge Instance { get; } = new();
    }

    public sealed class Eq : IOp<Eq>, IFloatOp<Eq>, IIntegerOp<Eq>
    {
        public static OpKind Kind => OpKind.eq;
        public static Eq Instance { get; } = new();
    }

    public sealed class Ne : IOp<Ne>, IFloatOp<Ne>, IIntegerOp<Ne>
    {
        public static OpKind Kind => OpKind.ne;
        public static Ne Instance { get; } = new();
    }
}

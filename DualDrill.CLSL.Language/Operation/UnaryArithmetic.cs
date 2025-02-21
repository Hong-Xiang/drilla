using DualDrill.Common;

namespace DualDrill.CLSL.Language.Operation;

public static class UnaryArithmetic
{
    public enum OpKind
    {
        neg
    }

    public interface IOp<TSelf> : IOpKind<TSelf, OpKind>, IUnaryOp<TSelf>
        where TSelf : IOp<TSelf>
    {
    }

    public sealed class Negate : IOp<Negate>, ISymbolOp<Negate>
    {
        public static OpKind Kind => OpKind.neg;

        public static Negate Instance { get; } = new();

        public string Symbol => "-";
    }
}
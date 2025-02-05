using DualDrill.Common;

namespace DualDrill.CLSL.Language.Operation;

public static class UnaryArithmetic
{
    public enum OpKind
    {
        neg
    }

    public interface IOp<TSelf> : IOpKind<TSelf, OpKind>, ISingleton<TSelf>, IUnaryOp
        where TSelf : IOp<TSelf>
    {
    }

    public sealed class Neg : IOp<Neg>, ISymbolOp<Neg>
    {
        public static OpKind Kind => OpKind.neg;

        public static Neg Instance { get; } = new();

        public static string Symbol => "-";
    }
}

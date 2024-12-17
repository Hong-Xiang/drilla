using DotNext.Patterns;
using DualDrill.CLSL.Language.Types;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;


public static class BinaryArithmetic
{
    public interface IOp
    {
        public Op Value { get; }
        public string Name { get; }

        abstract static IOp Instance { get; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Op
    {
        Addition,
        Subtraction,
        Multiplication,
        Division,
        Remainder
    }

    public struct Add : IOp
    {
        public static IOp Instance { get; } = new Add();

        public readonly Op Value => Op.Addition;
        public readonly string Name => "+";
    }
    public struct Sub : IOp
    {
        public static IOp Instance { get; } = new Sub();

        public readonly Op Value => Op.Subtraction;
        public readonly string Name => "-";
    }
    public struct Mul : IOp
    {
        public static IOp Instance { get; } = new Mul();

        public readonly Op Value => Op.Multiplication;

        public readonly string Name => "*";
    }
    public struct Div : IOp
    {
        public static IOp Instance { get; } = new Div();
        public readonly Op Value => Op.Division;
        public readonly string Name => "/";
    }
    public struct Rem : IOp
    {
        public static IOp Instance { get; } = new Rem();
        public readonly Op Value => Op.Remainder;
        public readonly string Name => "%";
    }

    public static IOp GetInstance(Op op)
    {
        return op switch
        {
            Op.Addition => Add.Instance,
            Op.Subtraction => Sub.Instance,
            Op.Multiplication => Mul.Instance,
            Op.Division => Div.Instance,
            Op.Remainder => Rem.Instance,
            _ => throw new InvalidEnumArgumentException(nameof(op), (int)op, typeof(Op))
        };
    }
}

public sealed record class BinaryArithmeticExpression(
    IExpression L,
    IExpression R,
    BinaryArithmetic.Op Op
) : IExpression
{
    public IShaderType Type => L.Type;
}

using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Literal;

public readonly record struct BoolLiteral(bool Value) : ILiteral<BoolLiteral, bool, BoolType>
{
}
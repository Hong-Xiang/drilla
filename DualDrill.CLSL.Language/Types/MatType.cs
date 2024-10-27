using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Types;

public sealed record class MatType(
    IScalarType ElementType,
    IRank Row,
    IRank Column)
    : IStorableType
{
    public string Name => $"mat{Row.Value}x{Column.Value}<{ElementType.Name}>";

    public int ByteSize => Row.Value * Column.Value * ElementType.ByteSize;
}

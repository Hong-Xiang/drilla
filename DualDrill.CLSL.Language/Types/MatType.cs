using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Types;

public sealed record class MatType(
    IScalarType ElementType,
    IRank Row,
    IRank Column)
    : IShaderType, IStorableType
{
    public string Name => $"mat{Row.Value}x{Column.Value}{ElementType.ElementName()}";

    public int ByteSize => Row.Value * Column.Value * ElementType.ByteSize;
}

using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Types;

enum MatrixMemoryLayout
{
    RowMajor,
    ColMajor
}

public sealed record class MatType(
    IScalarType ElementType,
    IRank Row,
    IRank Column)
    : IShaderType<MatType>, IStorableType
{
    public int ByteSize => Row.Value * Column.Value * ElementType.ByteSize;
    public string Name => $"mat{Row.Value}x{Column.Value}{ElementType.ElementName()}";

    public IRefType GetRefType() => throw new NotImplementedException();

    public IPtrType GetPtrType(IAddressSpace addressSpace) => throw new NotImplementedException();

    public T Evaluate<T>(IShaderTypeSemantic<T, T> semantic) => throw new NotImplementedException();

    public static MatType Instance => throw new NotImplementedException();

    public IPtrType GetPtrType() => throw new NotImplementedException();
}
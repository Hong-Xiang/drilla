namespace DualDrill.ApiGen.DMath;

public interface IDMathType
{
    public string Name { get; }
}


public interface IDMathScalarType : IDMathType
{
}

public enum Rank
{
    _2 = 2,
    _3 = 3,
    _4 = 4
}

public enum FloatBitWidth
{
    _16 = 16,
    _32 = 32,
    _64 = 64
}

public enum IntegerBitWidth
{
    _8 = 8,
    _16 = 16,
    _32 = 32,
    _64 = 64
}

public sealed record class BType() : IDMathScalarType
{
    public string Name => "bool";
}
public sealed record class FType(FloatBitWidth BitWidth) : IDMathScalarType
{
    public string Name => $"f{(int)BitWidth}";
}
public sealed record class CType(FloatBitWidth BitWidth) : IDMathScalarType
{
    public string Name => $"c{(int)BitWidth}";
}
public sealed record class QType(FloatBitWidth BitWidth) : IDMathScalarType
{
    public string Name => $"q{(int)BitWidth}";
}

public sealed record class IType(IntegerBitWidth BitWidth) : IDMathScalarType
{
    public string Name => $"i{(int)BitWidth}";
}
public sealed record class UType(IntegerBitWidth BitWidth) : IDMathScalarType
{
    public string Name => $"u{(int)BitWidth}";
}
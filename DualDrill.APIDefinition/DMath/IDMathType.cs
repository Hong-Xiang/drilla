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
    public string Name => $"f{BitWidth}";
}
public sealed record class CType(FloatBitWidth BitWidth) : IDMathScalarType
{
    public string Name => $"c{BitWidth}";
}
public sealed record class QType(FloatBitWidth BitWidth) : IDMathScalarType
{
    public string Name => $"q{BitWidth}";
}

public sealed record class IType(IntegerBitWidth BitWidth) : IDMathScalarType
{
    public string Name => $"i{BitWidth}";
}
public sealed record class UType(IntegerBitWidth BitWidth) : IDMathScalarType
{
    public string Name => $"u{BitWidth}";
}
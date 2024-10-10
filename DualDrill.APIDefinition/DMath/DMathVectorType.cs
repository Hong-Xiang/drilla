using System.Collections.Immutable;

namespace DualDrill.ApiGen.DMath;

public sealed record class DMathVectorType(IDMathScalarType ScalarType, Rank Size) : IDMathType
{
    public string Name => ScalarType switch
    {
        BType _ => $"vec{(int)Size}b",
        _ => $"vec{(int)Size}{ScalarType.Name}"
    };

    public ImmutableArray<string> Components => Size switch
    {
        Rank._2 => ["x", "y"],
        Rank._3 => ["x", "y", "z"],
        Rank._4 => ["x", "y", "z", "w"],
        _ => throw new NotSupportedException("")
    };
}

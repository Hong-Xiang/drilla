using System.Collections.Immutable;

namespace DualDrill.ApiGen.DMath;


public sealed record class DMathParameter(string Name, IDMathType Type)
{
}

public sealed record class DMathFunction(
    string Name,
    ImmutableArray<DMathParameter> Parameters,
    IDMathType ReturnType)
{
}


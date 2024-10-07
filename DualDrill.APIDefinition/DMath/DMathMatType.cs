namespace DualDrill.ApiGen.DMath;

public sealed record class DMathMatType(IDMathScalarType ScalarType, Rank Rows, Rank Columns) : IDMathType
{
    public string Name => $"mat{Rows}x{Columns}{ScalarType.Name}";
}


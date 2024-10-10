namespace DualDrill.ApiGen.DMath;

public sealed record class DMathMatType(IDMathScalarType ScalarType, Rank Rows, Rank Columns) : IDMathType
{
    public string Name => ScalarType switch
    {
        BType _ => $"mat{(int)Rows}x{(int)Columns}b",
        _ => $"mat{(int)Rows}x{(int)Columns}{ScalarType.Name}"
    };

    IEnumerable<string> ColumnFields => Columns switch
    {
        Rank._2 => ["x", "y"],
        Rank._3 => ["x", "y", "z"],
        Rank._4 => ["x", "y", "z", "w"],
        _ => throw new NotSupportedException()
    };
}


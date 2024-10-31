namespace DualDrill.Common.Nat;

public interface IBitWidth : INat
{
}

public sealed partial class N8 : IBitWidth
{
}

public sealed partial class N16 : IBitWidth
{
}

public sealed partial class N32 : IBitWidth
{
}

public sealed partial class N64 : IBitWidth
{
}

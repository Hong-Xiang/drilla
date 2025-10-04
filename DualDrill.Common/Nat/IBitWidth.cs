namespace DualDrill.Common.Nat;

public interface IBitWidth : INat
{
    abstract static IBitWidth BitWidth { get; }
}

public sealed partial class N8 : IBitWidth
{
    public static IBitWidth BitWidth => Instance;
}

public sealed partial class N16 : IBitWidth
{
    public static IBitWidth BitWidth => Instance;
}

public sealed partial class N32 : IBitWidth
{
    public static IBitWidth BitWidth => Instance;
}

public sealed partial class N64 : IBitWidth
{
    public static IBitWidth BitWidth => Instance;
}

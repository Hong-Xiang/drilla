namespace DualDrill.Common.Nat;

sealed class ValueVisitor : INatVisitor<int>
{
    public static readonly ValueVisitor Instance = new();
    public int Visit<TNat>(TNat n) where TNat : INat => n.Value;
}

public static partial class Nat
{
}

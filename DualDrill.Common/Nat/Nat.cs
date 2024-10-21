namespace DualDrill.Common.Nat;

sealed class ValueVisitor : INatVisitor<int>
{
    public static readonly ValueVisitor Instance = new();
    public int Visit<TNat>() where TNat : INat => TNat.Value;
}

public static partial class Nat
{
    public static int ToInt(this INat n) => n.Accept(ValueVisitor.Instance);
}

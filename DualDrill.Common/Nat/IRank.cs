namespace DualDrill.Common.Nat;

public interface IRank : INat
{
    public T Accept<T>(IVisitor<T> visitor);

    public interface IVisitor<T>
    {
        public T Visit<TRank>() where TRank : class, IRank<TRank>;
    }
}

public interface IRank<TSelf> : IRank, INat<TSelf>
    where TSelf : class, IRank<TSelf>
{
}

public sealed partial class N2 : IRank<N2>
{
    public T Accept<T>(IRank.IVisitor<T> visitor) => visitor.Visit<N2>();

}
public sealed partial class N3 : IRank<N3>
{
    public T Accept<T>(IRank.IVisitor<T> visitor) => visitor.Visit<N3>();
}
public sealed partial class N4 : IRank<N4>
{
    public T Accept<T>(IRank.IVisitor<T> visitor) => visitor.Visit<N4>();
}

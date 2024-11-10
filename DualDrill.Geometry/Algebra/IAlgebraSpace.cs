namespace DualDrill.Geometry.Algebra;

public interface IAlgebraSpace<TSelf>
    where TSelf : IAlgebraSpace<TSelf>
{
    public static abstract int Dimension { get; }
}

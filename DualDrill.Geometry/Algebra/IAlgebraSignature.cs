namespace DualDrill.Geometry.Algebra;

public interface IAlgebraSignature<TSelf> : IAlgebraSpace<TSelf>
    where TSelf : IAlgebraSignature<TSelf>
{
    public abstract static int Sign(int dim);
}

namespace DualDrill.Common;

public interface ISingleton<TSelf>
    where TSelf : ISingleton<TSelf>
{
    abstract static TSelf Instance { get; }
}

namespace DualDrill.Common;

public interface ISingleton<out TValue>
{
    static abstract TValue Instance { get; }
}

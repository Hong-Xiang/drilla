namespace DualDrill.GPU;

public interface IBackend<TBackend>
    where TBackend : IBackend<TBackend>
{
    abstract static GPUInstance<TBackend> Instance { get; }
}

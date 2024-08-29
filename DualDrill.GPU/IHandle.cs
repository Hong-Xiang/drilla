namespace DualDrill.GPU;

public interface IHandle<TBackend, TResource>
    where TBackend : IBackend<TBackend>
{
}

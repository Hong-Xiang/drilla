namespace DualDrill.GPU;

public sealed record class GPUInstance<TBackend>(IHandle<TBackend, GPUInstance<TBackend>> Handle)
    where TBackend : IBackend<TBackend>
{
    // TODO: add language features api

    public ValueTask<GPUAdaptor<TBackend>> RequestAdapterAsync(
        GPURequestAdapterOptions options,
        CancellationToken cancellationToken)
    {
        
    }
}

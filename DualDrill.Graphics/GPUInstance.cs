namespace DualDrill.Graphics;

public sealed partial record class GPUInstance<TBackend>
{
    public ValueTask<GPUAdapter<TBackend>> RequestAdapterAsync(
        GPURequestAdapterOptions options,
        CancellationToken cancellationToken)
    {
        return TBackend.Instance.RequestAdapterAsync(this, options, cancellationToken);
    }
}

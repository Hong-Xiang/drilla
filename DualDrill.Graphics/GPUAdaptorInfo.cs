namespace DualDrill.Graphics;

public readonly record struct GPUAdapterInfo(
    string Vendor,
    string Architecture,
    string Device,
    string Description
)
{
    public GPUBackendType BackendType { get; init; } = GPUBackendType.Undefined;
    public GPUAdapterType AdapterType { get; init; } = GPUAdapterType.Unknown;
}
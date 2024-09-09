namespace DualDrill.Graphics;

public readonly record struct GPUAdapterInfo(
    string Vendor,
    string Architecture,
    string Device,
    string Description
)
{
}
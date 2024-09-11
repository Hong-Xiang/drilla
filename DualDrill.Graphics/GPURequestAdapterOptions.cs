namespace DualDrill.Graphics;

public partial struct GPURequestAdapterOptions
{
    public bool ForceFallbackAdapter { get; set; }
    public required GPUPowerPreference PowerPreference { get; set; }
    public GPUBackendType BackendType { get; set; }
    public IGPUSurface? CompatibleSurface { get; set; }
}

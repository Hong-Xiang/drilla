namespace DualDrill.GPU;

public struct GPURequestAdapterOptions()
{
    public bool ForceFallbackAdapter { get; set; }
    public GPUPowerPreference PowerPreference { get; set; }
    public GPUBackendType BackendType { get; set; }
    public GPUSurface CompatibleSurface { get; set; }
}


namespace DualDrill.Graphics;

public unsafe partial struct GPUDeviceDescriptor
{
    public ReadOnlyMemory<GPUFeatureName> RequiredFeatures { get; set; }
    public delegate* unmanaged[Cdecl]<GPUDeviceLostReason, sbyte*, void*, void> DeviceLostCallback { get; set; }
    public nint DeviceLostUserdata { get; set; }
    public ReadOnlyMemory<GPURequiredLimits> RequiredLimits { get; set; }
    public string? Label { get; set; }
    public GPUQueueDescriptor DefaultQueue { get; set; }
}

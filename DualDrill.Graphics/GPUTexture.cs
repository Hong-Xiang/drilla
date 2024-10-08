﻿namespace DualDrill.Graphics;

public partial interface IGPUTexture : IDisposable
{
    string Label { get; }
    int Width { get; }
    int Height { get; }
    int DepthOrArrayLayers { get; }
    int MipLevelCount { get; }
    int SampleCount { get; }
    GPUTextureDimension Dimension { get; }
    GPUTextureFormat Format { get; }
    GPUTextureUsage Usage { get; }
    IGPUTextureView CreateView(GPUTextureViewDescriptor? descriptor = default);
}

public sealed partial record class GPUTexture<TBackend>(GPUHandle<TBackend, GPUTexture<TBackend>> Handle)
    : IDisposable, IGPUTexture
    where TBackend : IBackend<TBackend>
{
    public string Label { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public int DepthOrArrayLayers { get; init; }

    public int MipLevelCount { get; init; }

    public int SampleCount { get; init; }

    public GPUTextureDimension Dimension { get; init; }

    public GPUTextureFormat Format { get; init; }

    public GPUTextureUsage Usage { get; init; }

    public IGPUTextureView CreateView(GPUTextureViewDescriptor? descriptor = default)
    {
        return TBackend.Instance.CreateView(this, descriptor);
    }

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}



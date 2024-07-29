﻿
using DualDrill.Graphics;

namespace DualDrill.Server;

public sealed class WGPUProviderService : IAsyncDisposable
{
    public GPUInstance Instance { get; }
    public GPUAdapter Adapter { get; }
    public GPUDevice Device { get; }
    public GPUQueue Queue { get; }

    public WGPUProviderService()
    {
        Instance = new GPUInstance();
        Adapter = Instance.RequestAdapter(null);
        Device = Adapter.RequestDevice();
        Queue = Device.GetQueue();
    }

    public async ValueTask DisposeAsync()
    {
        Queue.Dispose();
        Device.Dispose();
        Adapter.Dispose();
        Instance.Dispose();
    }
}

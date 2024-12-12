using System.Runtime.InteropServices;

namespace DualDrill.Graphics;

public readonly struct DisposableGCHandle(GCHandle Handle) : IDisposable
{
    public GCHandle Handle { get; } = Handle;
    public void Dispose()
    {
        if (Handle.IsAllocated)
        {
            Handle.Free();
        }
    }
}

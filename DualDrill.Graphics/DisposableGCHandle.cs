using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

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

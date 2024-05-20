using Silk.NET.WebGPU;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics;

public sealed class Buffer(
    Silk.NET.WebGPU.WebGPU Api,
    NativeHandle<Silk.NET.WebGPU.Buffer> Handle)
{
    public unsafe Silk.NET.WebGPU.Buffer* Ptr { get; } = Handle.Value;

    public unsafe void Unmap()
    {
        Api.BufferUnmap(Handle);
    }

    public unsafe Task MapAsync(MapMode mapMode, int offset, int size)
    {
        var tcs = new TaskCompletionSource();
        Api.BufferMapAsync(Handle, mapMode, (nuint)offset, (nuint)size, new PfnBufferMapCallback((status, data) =>
        {
            if (status == BufferMapAsyncStatus.Success)
            {
                tcs.SetResult();
            }
            else
            {
                Console.WriteLine($"Map failed with status ${Enum.GetName(status)}");
            }
        }), default);
        return tcs.Task;
    }
}

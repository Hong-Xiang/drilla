using Silk.NET.Core.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics;

public unsafe ref struct NativeStringRef(byte* Handle)
{
    public byte* Handle { get; } = Handle;

    public static NativeStringRef Create(string data)
    {
        return new NativeStringRef((byte*)SilkMarshal.StringToPtr(data));
    }

    public void Dispose()
    {
        SilkMarshal.Free((nint)Handle);
    }
}

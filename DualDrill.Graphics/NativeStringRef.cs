using Silk.NET.Core.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics;

public unsafe readonly struct NativeStringRef(byte* Handle) : IDisposable
{
    public byte* Handle { get; } = Handle;

    public static implicit operator byte*(NativeStringRef self) => self.Handle;

    public static NativeStringRef Create(string data)
    {
        return new NativeStringRef((byte*)SilkMarshal.StringToPtr(data));
    }

    public void Dispose()
    {
        if ((nint)Handle != 0)
        {
            SilkMarshal.Free((nint)Handle);
        }
    }
}
public unsafe ref struct NativeStringArrayRef(byte** Handle)
{
    public byte** Handle { get; } = Handle;
    public static implicit operator byte**(NativeStringArrayRef self) => self.Handle;

    public static NativeStringArrayRef Create(IReadOnlyList<string> data)
    {
        return new NativeStringArrayRef((byte**)SilkMarshal.StringArrayToPtr(data));
    }

    public void Dispose()
    {
        SilkMarshal.Free((nint)Handle);
    }
}

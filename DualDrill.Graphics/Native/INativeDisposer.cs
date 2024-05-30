using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics.Native;

internal interface INativeDisposer<T>
    where T : unmanaged
{
    unsafe abstract static void NativeDispose(T* handle);
}

internal unsafe sealed class NativeHandle<TApi, T> : SafeHandleZeroOrMinusOneIsInvalid
    where T : unmanaged
    where TApi : INativeDisposer<T>
{
    public NativeHandle(T* Pointer) : base(true)
    {
        handle = (nint)Pointer;
        if (IsInvalid)
        {
            throw new GraphicsApiException($"Native resource invalid {nameof(T)} handle");
        }
    }
    public static implicit operator T*(NativeHandle<TApi, T> handle)
    {
        return (T*)handle.handle;
    }
    protected override bool ReleaseHandle()
    {
        TApi.NativeDispose((T*)handle);
        return true;
    }

    public override string ToString()
    {
        return $"{nameof(NativeHandle<TApi, T>)}({handle:X})";
    }
}

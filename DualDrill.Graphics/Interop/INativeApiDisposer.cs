using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics.Interop;

internal interface INativeApiDisposer<T>
    where T : unmanaged
{
    unsafe abstract static void NativeDispose(T* handle);
}

internal unsafe sealed class NativeHandle<TNativeDisposer, T> : SafeHandleZeroOrMinusOneIsInvalid
    where T : unmanaged
    where TNativeDisposer : INativeApiDisposer<T>
{
    public NativeHandle(T* pointer) : base(true)
    {
        handle = (nint)pointer;
        if (IsInvalid)
        {
            throw new GraphicsApiException($"Native resource invalid {nameof(T)} handle");
        }
    }
    public static implicit operator T*(NativeHandle<TNativeDisposer, T> handle)
    {
        return (T*)handle.handle;
    }
    protected override bool ReleaseHandle()
    {
        TNativeDisposer.NativeDispose((T*)handle);
        return true;
    }

    public override string ToString()
    {
        return $"{nameof(NativeHandle<TNativeDisposer, T>)}({handle:X})";
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics;

public readonly unsafe struct NativeHandle<T>(T* Ptr)
    where T : unmanaged
{
    public T* Value { get; } = Ptr;

    public static implicit operator T*(NativeHandle<T> ptr)
    {
        return ptr.Value;
    }

    public static implicit operator NativeHandle<T>(T* ptr)
    {
        return new(ptr);
    }
}

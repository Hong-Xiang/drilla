using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text;

namespace DualDrill.Interop;

public unsafe readonly struct PinnedNullTerminatedUtf8String : IDisposable
{
    readonly MemoryHandle Handle;
    internal PinnedNullTerminatedUtf8String(ReadOnlyMemory<byte> data)
    {
        Handle = data.Pin();
    }
    public void Dispose()
    {
        Handle.Dispose();
    }
    public sbyte* Pointer => (sbyte*)Handle.Pointer;
}

public unsafe sealed class InteropUtf8StringValue : IDisposable
{
    nint Buffer = 0;
    InteropUtf8StringValue(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            var size = Encoding.UTF8.GetByteCount(value) + 1;
            Buffer = Marshal.AllocHGlobal(size);
            Encoding.UTF8.GetBytes(value, new Span<byte>((void*)Buffer, size));
            ((byte*)Buffer)[size - 1] = 0;
        }
    }

    public char* CharPointer => (char*)Buffer;
    public sbyte* SbytePointer => (sbyte*)Buffer;
    public byte* BytePointer => (byte*)Buffer;

    public static InteropUtf8StringValue Create(string? value = default) => new(value);

    public void Dispose()
    {
        if (Buffer is not 0)
        {
            Marshal.FreeHGlobal(Buffer);
            Buffer = 0;
        }
    }
}


public readonly struct InteropUtf8String
{
    public static InteropUtf8String Create(string data)
    {
        return Create(Encoding.UTF8.GetBytes(data));
    }

    public static InteropUtf8String Create(ReadOnlySpan<byte> utf8BytesWithoutTerminatingZero)
    {
        return new(utf8BytesWithoutTerminatingZero);
    }

    public static implicit operator InteropUtf8String(string value) => Create(value);
    public static implicit operator InteropUtf8String(ReadOnlySpan<byte> u8Value) => Create(u8Value);

    private byte[]? Buffer { get; } = null;

    public int ContentByteLength => Buffer is not null ? Buffer.Length - 1 : 0;

    public PinnedNullTerminatedUtf8String Pin()
    {
        ReadOnlyMemory<byte> memory = new();
        if (Buffer is not null)
        {
            memory = Buffer.AsMemory();
        }
        return new PinnedNullTerminatedUtf8String(memory);
    }

    private InteropUtf8String(ReadOnlySpan<byte> utf8Bytes)
    {
        if (utf8Bytes.Length == 0)
        {
        }
        else
        {
            Buffer = new byte[utf8Bytes.Length + 1];
            utf8Bytes.CopyTo(Buffer.AsSpan()[..ContentByteLength]);
        }
    }
}

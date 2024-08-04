using System.Buffers;
using System.Collections.Immutable;
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


public sealed class Utf8String
{
    public static Utf8String Create(string data)
    {
        return Create(Encoding.UTF8.GetBytes(data));
    }

    public static Utf8String Create(ReadOnlySpan<byte> utf8BytesWithoutTerminatingZero)
    {
        return new(utf8BytesWithoutTerminatingZero);
    }

    public ReadOnlyMemory<byte> Memory { get; }

    private readonly ImmutableArray<byte> Bytes;

    public int ByteLength { get; }

    public PinnedNullTerminatedUtf8String Pin()
    {
        return new PinnedNullTerminatedUtf8String(Bytes.AsMemory());
    }

    private Utf8String(ReadOnlySpan<byte> utf8Bytes)
    {
        ByteLength = utf8Bytes.Length;
        var buffer = new byte[ByteLength + 1];
        utf8Bytes.CopyTo(buffer.AsSpan()[..ByteLength]);
        Bytes = [.. buffer];
        Memory = Bytes.AsMemory()[..ByteLength];
    }

    public static implicit operator Utf8String(string data) => Create(data);
}

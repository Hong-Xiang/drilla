namespace DualDrill.Engine.WebRTC;

public interface IRTCDataChannel
{
    ValueTask Send(Span<byte> data);
    IAsyncEnumerable<Memory<byte>> Message { get; }
}

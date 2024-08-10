using R3;

namespace DualDrill.Engine.Connection;

public interface IDataChannel : IDisposable
{
    string Label { get; }
    int Id { get; }
    void Send(ReadOnlySpan<byte> data);
    Observable<ReadOnlyMemory<byte>> OnMessage { get; }
}
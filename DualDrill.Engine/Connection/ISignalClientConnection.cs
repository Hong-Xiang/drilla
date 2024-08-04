namespace DualDrill.Engine.Connection;

public interface ISignalClientConnection
{
    ValueTask RequestNegotiate();
    ValueTask AddIceCandidate(string candidate);
    IObservable<string> OnIceCandidate { get; }
}

public interface IDualDrillConnection : IDisposable
{
    IDataChannel CreateDataChannel(string label);
}

public interface IDataChannel : IDisposable
{
    string Label { get; }
    void Send(ReadOnlySpan<byte> data);
    IObservable<ReadOnlyMemory<byte>> OnMessage { get; }
}
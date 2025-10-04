using DualDrill.Engine.Connection;
using MessagePipe;
using R3;
using SIPSorcery.Net;

namespace DualDrill.Server.Services;

sealed class SIPSorceryDataChannel : IDataChannel
{
    private Subject<ReadOnlyMemory<byte>> OnMessageSubject = new();
    private RTCDataChannel DataChannel { get; }
    public string Label => DataChannel.label;
    public int Id => (int)DataChannel.id;

    public SIPSorceryDataChannel(RTCDataChannel dataChannel)
    {
        DataChannel = dataChannel;
        DataChannel.onmessage += OnDataChannelMessage;
        OnMessage = OnMessageSubject.AsObservable();
    }

    public void Send(ReadOnlySpan<byte> data)
    {
        DataChannel.send(data.ToArray());
    }
    public Observable<ReadOnlyMemory<byte>> OnMessage { get; }

    public void Dispose()
    {
        DataChannel.onmessage -= OnDataChannelMessage;
        OnMessageSubject.Dispose();
    }
    private void OnDataChannelMessage(RTCDataChannel dc, DataChannelPayloadProtocols protocol, byte[] data)
    {
        OnMessageSubject.OnNext(data);
    }
}

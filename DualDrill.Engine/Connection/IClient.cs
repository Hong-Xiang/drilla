using DualDrill.Engine.WebRTC;
using MessagePipe;
using System.Threading.Channels;

namespace DualDrill.Engine.Connection;

public interface IClient
{
    public Guid Id { get; }
    public Uri Uri { get; }
    public IPeerConnection PeerConnection { get; }
    public ValueTask<string> GetConnectionId();
    public ValueTask<IRTCPeerConnection> CreatePeerConnection();
    public ValueTask SendDataStream<T>(Uri uri, IAsyncEnumerable<T> dataStream);
    public ValueTask<IAsyncEnumerable<T>> SubscribeDataStream<T>(Uri uri);
    public ValueTask HubInvokeAsync(Func<object, ValueTask> func);
    public Channel<object> GetOrAddEventChannel(Uri uri);
    ISubscriber<IClient> OnPeerConnected { get; }
}

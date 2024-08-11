using DualDrill.Engine.WebRTC;
using MessagePipe;
using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace DualDrill.Engine.Connection;

public interface IClient
{
    public Guid Id { get; }
    public Uri Uri { get; }

    [JsonIgnore]
    public IPeerConnection PeerConnection { get; }
    public ValueTask<string> GetConnectionId();
    public ValueTask<IRTCPeerConnection> CreatePeerConnection();
    public ValueTask SendDataStream<T>(Uri uri, IAsyncEnumerable<T> dataStream);
    public ValueTask<IAsyncEnumerable<T>> SubscribeDataStream<T>(Uri uri);
    public ValueTask HubInvokeAsync(Func<object, ValueTask> func);
    public Channel<object> GetOrAddEventChannel(Uri uri);
    [JsonIgnore]
    ISubscriber<IClient> OnPeerConnected { get; }
}

using DualDrill.Engine.WebRTC;
using MessagePipe;
using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace DualDrill.Engine.Connection;

public sealed record class ClientIdentity(Guid Id) : IClient
{
    public Uri Uri { get; } = new($"uuid:{Id}");

    [JsonIgnore]
    public IPeerConnection PeerConnection => throw new NotImplementedException();

    [JsonIgnore]
    public ISubscriber<IClient> OnPeerConnected => throw new NotImplementedException();


    public ValueTask<IRTCPeerConnection> CreatePeerConnection()
    {
        throw new NotImplementedException();
    }

    public ValueTask<string> GetConnectionId()
    {
        throw new NotImplementedException();
    }

    public Channel<object> GetOrAddEventChannel(Uri uri)
    {
        throw new NotImplementedException();
    }

    public ValueTask HubInvokeAsync(Func<object, ValueTask> func)
    {
        throw new NotImplementedException();
    }

    public ValueTask SendDataStream<T>(Uri uri, IAsyncEnumerable<T> dataStream)
    {
        throw new NotImplementedException();
    }

    public ValueTask<IAsyncEnumerable<T>> SubscribeDataStream<T>(Uri uri)
    {
        throw new NotImplementedException();
    }
}

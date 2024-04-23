using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Engine.Connection;

public sealed class NotPartitionClientException(IClient RequestedClient) : Exception
{
    public IClient RequestedClient { get; } = RequestedClient;
}

public enum PairRole
{
    Source,
    Target
}

public interface IConnectedPair<T> : IAsyncDisposable
{
    T Source { get; }
    T Target { get; }
    IClient SourceClient { get; }
    IClient TargetClient { get; }
}

public static class ConnectedPairExtension
{
    public static T GetSelf<T>(this IConnectedPair<T> pair, IClient client)
    {
        return pair.GetRole(client) switch
        {
            PairRole.Source => pair.Source,
            PairRole.Target => pair.Target,
            _ => throw new NotPartitionClientException(client),
        };
    }

    public static T GetPeer<T>(this IConnectedPair<T> pair, IClient client)
    {
        return pair.GetRole(client) switch
        {
            PairRole.Source => pair.Target,
            PairRole.Target => pair.Source,
            _ => throw new NotPartitionClientException(client),
        };
    }

    public static PairRole GetRole<T>(this IConnectedPair<T> pair, IClient client)
    {
        if (client.Equals(pair.SourceClient))
        {
            return PairRole.Source;
        }
        if (client.Equals(pair.TargetClient))
        {
            return PairRole.Target;
        }
        throw new NotPartitionClientException(client);
    }
}

public interface IDataChannelReference
{
}

public interface IDataChannelReferencePair : IConnectedPair<IDataChannelReference>
{
}



public interface IClientVideoReference
{
    IClient Client { get; }
    string Id { get; }
}

public interface IVideoChannelReferencePair : IConnectedPair<IClientVideoReference>
{
}

public interface IP2PClientPair : IConnectedPair<IClient>
{
    Task<IDataChannelReferencePair> CreateDataChannel(string label);
    Task<IClientVideoReference> SendVideo(IClientVideoReference video);
    IObservable<IClientVideoReference> VideoReceived { get; }
}


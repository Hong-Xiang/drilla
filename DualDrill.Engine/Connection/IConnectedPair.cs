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

public interface IConnectedPair<out TSourceClient, out TTargetClient, T> : IAsyncDisposable
    where TSourceClient : IClient
    where TTargetClient : IClient
{
    T Source { get; }
    T Target { get; }
    TSourceClient SourceClient { get; }
    TTargetClient TargetClient { get; }
}

public static class ConnectedPairExtension
{
    public static T GetSelf<T>(this IConnectedPair<IClient, IClient, T> pair, IClient client)
    {
        return pair.GetRole(client) switch
        {
            PairRole.Source => pair.Source,
            PairRole.Target => pair.Target,
            _ => throw new NotPartitionClientException(client),
        };
    }

    public static T GetPeer<T>(this IConnectedPair<IClient, IClient, T> pair, IClient client)
    {
        return pair.GetRole(client) switch
        {
            PairRole.Source => pair.Target,
            PairRole.Target => pair.Source,
            _ => throw new NotPartitionClientException(client),
        };
    }

    public static PairRole GetRole<T>(this IConnectedPair<IClient, IClient, T> pair, IClient client)
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

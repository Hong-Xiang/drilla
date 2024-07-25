using MessagePipe;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reactive.Subjects;

namespace DualDrill.Engine.Connection;

public sealed class ClientStore(
    ILogger<ClientStore> Logger,
    IPublisher<ImmutableArray<IClient>> ClientConnectionChanged) : IDisposable
{

    readonly ConcurrentDictionary<Uri, IClient> ClientsStore = new();

    readonly BehaviorSubject<ImmutableArray<IClient>> ClientsSubject = new([]);

    public IClient? GetClient(Uri id)
    {
        if (ClientsStore.TryGetValue(id, out var client))
        {
            return client;
        }
        else
        {
            return null;
        }
    }
    public void AddClient(IClient client)
    {
        if (!ClientsStore.TryAdd(client.Uri, client))
        {
            Logger.LogError("Failed to add client with id {ClientUri}", client.Uri);
        }
        ClientConnectionChanged.Publish(Clients);
        OnClientChanges?.Invoke(Clients);
    }

    public void RemoveClient(IClient client)
    {
        if (!ClientsStore.TryRemove(client.Uri, out var _))
        {
            Logger.LogError("Failed to remove client with uri {ClientUri}", client.Uri);
        }
        ClientConnectionChanged.Publish(Clients);
        OnClientChanges?.Invoke(Clients);
    }

    public Uri[] ClientUris => [.. ClientsStore.Keys];

    public delegate void ClientsChanges(ImmutableArray<IClient> clients);
    public event ClientsChanges OnClientChanges;

    public ImmutableArray<IClient> Clients => [.. ClientsStore.Values];

    //public Task<IPeerToPeerClientPair> CreatePeerPairAsync(IClient source, IClient target)
    //{
    //    return source.CreatePairAsync(target);
    //}

    public void Dispose()
    {
        ClientsSubject.Dispose();
    }
}

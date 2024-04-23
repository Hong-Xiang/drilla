using DualDrill.Engine.UI;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Engine.Connection;

public sealed class ClientStore(ILogger<ClientStore> Logger) : IDisposable
{
    readonly ConcurrentDictionary<string, IClient> ClientsStore = new();

    readonly BehaviorSubject<ImmutableArray<IClient>> ClientsSubject = new([]);

    public IClient? GetClient(string id)
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
    public void AddClient(string id, IClient client)
    {
        if (!ClientsStore.TryAdd(id, client))
        {
            Logger.LogError("Failed to add client with id {ClientId}", id);
        }
        ClientsSubject.OnNext([.. ClientsStore.Values]);
    }

    public void RemoveClient(IClient client)
    {
        if (!ClientsStore.TryRemove(client.Id, out var _))
        {
            Logger.LogError("Failed to remove client with id {0}", client.Id);
        }
        ClientsSubject.OnNext([.. ClientsStore.Values]);
    }

    public string[] ClientIds => [.. ClientsStore.Keys];

    public IObservable<ImmutableArray<IClient>> Clients => ClientsSubject;

    public Task<IP2PClientPair> CreatePeerPairAsync(IClient source, IClient target)
    {
        return source.CreatePairAsync(target);
    }

    public void Dispose()
    {
        ClientsSubject.Dispose();
    }
}

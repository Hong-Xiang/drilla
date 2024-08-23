using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reactive.Subjects;

namespace DualDrill.Engine.Connection;

public sealed partial class ClientsManager(
    ILogger<ClientsManager> Logger,
    IPublisher<ImmutableArray<IClient>> ClientConnectionChanged) : IDisposable
{
    public static Guid ServerId { get; } = Guid.Empty;

    readonly ConcurrentDictionary<Uri, IClient> ClientsStore = new();
    readonly ConcurrentDictionary<Uri, AsyncServiceScope> Scopes = [];

    readonly ConcurrentDictionary<Guid, string> ConnectionIds = new();

    readonly BehaviorSubject<ImmutableArray<IClient>> ClientsSubject = new([]);

    public void UpdateConnectionId(Guid clientId, string connectionId)
    {
        if (!ConnectionIds.TryAdd(clientId, connectionId))
        {
            LogFailedToAddConnectionId(Logger, clientId, connectionId);
        }
    }

    public string? GetConnectionId(Guid clientId)
    {
        if (ConnectionIds.TryGetValue(clientId, out var result))
        {
            return result;
        }
        LogConnectionIdNotFound(Logger, clientId);
        return null;
    }

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
    }

    public void RemoveClient(IClient client)
    {
        if (!ClientsStore.TryRemove(client.Uri, out var _))
        {
            Logger.LogError("Failed to remove client with uri {ClientUri}", client.Uri);
        }
        ClientConnectionChanged.Publish(Clients);
    }

    public Uri[] ClientUris => [.. ClientsStore.Keys];

    public ImmutableArray<IClient> Clients => [.. ClientsStore.Values];

    public void Dispose()
    {
        ClientsSubject.Dispose();
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to update connection id for {ClientId}, connection id {ConnectionId}"
    )]
    static partial void LogFailedToAddConnectionId(ILogger logger, Guid clientId, string connectionId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Connection id for {ClientId} not found"
    )]
    static partial void LogConnectionIdNotFound(ILogger logger, Guid clientId);
}

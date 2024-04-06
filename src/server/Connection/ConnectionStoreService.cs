using System.Collections.Concurrent;

namespace Drill.Connection;

sealed class ConnectionStoreService(ILogger<ConnectionStoreService> Logger)
{
    private readonly ConcurrentBag<string> ClientIdsBag = [];
    public IEnumerable<string> ClientIds => ClientIdsBag;

    public void AddClient(string connectionId)
    {
        ClientIdsBag.Add(connectionId);
    }

    public void RemoveClient(string connectionId)
    {
        var removed = !ClientIdsBag.TryTake(out var _);
        if (!removed)
        {
            Logger.LogWarning("Failed to remove client with id {ConnectionId}", connectionId);
        }
    }
}

using System.Collections.Concurrent;

namespace DualDrill.Server.BrowserClient;
sealed class ClientsStore
{
    private readonly ConcurrentDictionary<string, BrowserClient> Clients = new();

    public void Add(string id, BrowserClient client)
    {
        Clients.TryAdd(id, client);
    }

    public void Remove(string id)
    {
        Clients.Remove(id, out var _);
    }

    public BrowserClient? Get(string id)
    {
        if (Clients.TryGetValue(id, out var c))
        {
            return c;
        }
        else
        {
            return null;
        }
    }

    public string[] ClientIds() => [.. Clients.Keys];
}

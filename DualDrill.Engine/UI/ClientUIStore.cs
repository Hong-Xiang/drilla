using DualDrill.Engine.Connection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Engine.UI;

public sealed class ClientUIStore(ILogger<ClientUIStore> Logger)
{
    readonly ConcurrentDictionary<IClient, IUIClient> ClientUIRoots = new();

    public IUIClient? GetUIRoot(IClient client)
    {
        if (ClientUIRoots.TryGetValue(client, out var clientUIRoot))
        {
            return clientUIRoot;
        }
        else
        {
            return null;
        }
    }

    public void AddUIRoot(IClient client, IUIClient uiRoot)
    {
        if (!ClientUIRoots.TryAdd(client, uiRoot))
        {
            Logger.LogError("Failed to add client with id {ClientId}", client.Id);
        }
    }
}

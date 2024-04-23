using DualDrill.Engine.Connection;
using DualDrill.Server.BrowserClient;
using DualDrill.Server.Components.Pages;

namespace DualDrill.Server.Application;

internal interface IDesktopBrowserUI : IPeerVideoUI
{
    ValueTask SetPeerClient(IClient client);
    ValueTask RenderUpdatedState();
}


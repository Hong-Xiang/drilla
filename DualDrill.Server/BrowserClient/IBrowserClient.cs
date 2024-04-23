using DualDrill.Engine.Connection;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace DualDrill.Server.BrowserClient;

public interface IBrowserClient : IClient
{
    public Circuit Circuit { get; }
    public JSDrillClientModule Module { get; }
    public ValueTask<JSMediaStreamProxy> GetCameraStreamAsync();
}

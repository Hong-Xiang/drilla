using DualDrill.Engine.Connection;
using DualDrill.Engine.WebRTC;

namespace DualDrill.Server.Command;

readonly struct ShowPeerClientCommand(IClient PeerClient, RTCPeerConnectionPair ConnectionPair) : IClientAsyncCommand<IClient>
{
    public readonly async ValueTask ExecuteAsyncOn(IClient client)
    {
        if (client is Browser.BrowserClient bc)
        {
            var ui = bc.UserInterface;
            if (ui is not null)
            {
                await ui.SetPeerClient(PeerClient, ConnectionPair).ConfigureAwait(false);
            }
        }
    }
}

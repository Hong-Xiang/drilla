using DualDrill.Engine.Connection;

namespace DualDrill.Server.Command;

readonly struct ShowPeerClientCommand(IClient PeerClient) : IClientAsyncCommand<IClient>
{
    public readonly async ValueTask ExecuteAsyncOn(IClient client)
    {
        if (client is Browser.BrowserClient bc)
        {
            var ui = bc.UserInterface;
            if (ui is not null)
            {
                await ui.SetPeerClient(PeerClient).ConfigureAwait(false);
            }
        }
    }
}

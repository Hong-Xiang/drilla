using DualDrill.Engine.Connection;
using DualDrill.Server.Application;
using DualDrill.Server.Browser;
using Microsoft.JSInterop;

namespace DualDrill.Server.Command;

public class GetRenderCanvas : IClientAsyncCommand<IClient, IJSObjectReference>
{
    public async ValueTask<IJSObjectReference> ExecuteAsyncOn(IClient client)
    {
        if (client is BrowserClient bs && bs.UserInterface is IDesktopBrowserUI ui)
        {
            return await ui.GetCanvasElement();
        }
        throw new NotSupportedException();
    }
}

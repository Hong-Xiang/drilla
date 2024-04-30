using DualDrill.Engine.Connection;
using DualDrill.Engine.UI;
using DualDrill.Engine.WebRTC;
using DualDrill.Server.Application;

namespace DualDrill.Server.Command;

readonly struct CloseSelfVideoCommand() : IClientAsyncCommand<IClient>
{
    public readonly async ValueTask ExecuteAsyncOn(IClient client)
    {
        Console.WriteLine($"Execute {nameof(CloseSelfVideoCommand)}");

        if (client is Browser.BrowserClient bc && bc.UserInterface is IDesktopBrowserUI ui)
        {
            await ui.CloseSelfVideo();
        }
    }
}
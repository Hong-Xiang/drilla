using DualDrill.Engine.Connection;
using DualDrill.Engine.UI;
using DualDrill.Engine.WebRTC;
using DualDrill.Server.Application;
using DualDrill.Server.Components.Pages;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.JSInterop;

namespace DualDrill.Server.Command;
readonly struct ClosePeerVideoElementCommand() : IClientAsyncCommand<IClient>
{
    public async ValueTask ExecuteAsyncOn(IClient client)
    {
        Console.WriteLine($"Execute {nameof(ClosePeerVideoElementCommand)}");
        if (client is Browser.BrowserClient bc && bc.UserInterface is IDesktopBrowserUI ui)
        {
            await ui.ClosePeerVideo();
        }
    }
}

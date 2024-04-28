using DualDrill.Engine.Connection;
using DualDrill.Engine.UI;
using DualDrill.Engine.WebRTC;
using DualDrill.Server.Application;
using DualDrill.Server.Components.Pages;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.JSInterop;

namespace DualDrill.Server.Command;
readonly struct ShowPeerVideoElementCommand(IMediaStream Stream) : IClientAsyncCommand<IClient>
{
    public async ValueTask ExecuteAsyncOn(IClient client)
    {
        Console.WriteLine($"Execute {nameof(ShowPeerClientCommand)}");
        if (client is BrowserClient.BrowserClient bc && bc.UserInterface is IDesktopBrowserUI ui)
        {
            await ui.ShowPeerVideo(Stream);
        }
    }
}

//sealed class BrowserUIClient(BrowserClient HeadlessClient, MediaDevices MediaDevices, IDesktopBrowserUI UI)
//    : IBrowserClient, IUserInterfaceClient
//{
//    public string Id => HeadlessClient.Id;
//    public Circuit Circuit => HeadlessClient.Circuit;
//    public JSDrillClientModule Module => HeadlessClient.Module;

//    public static async Task<BrowserUIClient> CreateAsync(
//        IServiceProvider services,
//        Circuit circuit,
//        DesktopBrowserClient ui
//    )
//    {
//        var client = await BrowserClient.CreateAsync(
//            services,
//            circuit,
//            services.GetRequiredService<IJSRuntime>()
//        );
//        return new(client, services.GetRequiredService<MediaDevices>(), ui);
//    }


//    public BrowserClient HeadlessClient { get; } = HeadlessClient;
//    public MediaDevices MediaDevices { get; } = MediaDevices;
//    public IDesktopBrowserUI UI { get; } = UI;


//    public bool Equals(IClient? other) => other?.Id == Id;

//    public async ValueTask<JSMediaStreamProxy> GetCameraStreamAsync()
//    {
//        return await MediaDevices.GetUserMedia(this, audio: false, video: true);
//    }

//    public ValueTask<IVideoElement> GetSelfVideoElementAsync() => UI.GetSelfVideoElement();

//    public ValueTask<IVideoElement> GetPeerVideoElementAsync() => UI.GetPeerVideoElement();

//    public async ValueTask<TResult> ExecuteCommand<TResult>(IUserInterfaceAsyncCommand<TResult> command)
//    {
//        return command switch
//        {
//            GetSelfVideoElement => (TResult)await UI.GetSelfVideoElement(),
//            GetPeerVideoElement => (TResult)await UI.GetPeerVideoElement(),
//            _ => throw new NotImplementedException(command.ToString()),
//        };
//    }

//    public ValueTask<IRTCPeerConnection> CreatePeerConnection()
//    {
//        throw new NotImplementedException();
//    }
//}

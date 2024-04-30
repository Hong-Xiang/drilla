using DualDrill.Engine.Connection;
using DualDrill.Engine.WebRTC;
using Microsoft.JSInterop;

namespace DualDrill.Server.Application;

internal interface IDesktopBrowserUI
{
    ValueTask<IJSObjectReference> GetCanvasElement();
    ValueTask SetPeerClient(IClient client);
    ValueTask ShowPeerVideo(IMediaStream stream);
    ValueTask ShowSelfVideo(IMediaStream stream);
}


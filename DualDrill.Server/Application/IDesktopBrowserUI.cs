using DualDrill.Engine.Connection;
using DualDrill.Engine.WebRTC;

namespace DualDrill.Server.Application;

internal interface IDesktopBrowserUI
{
    ValueTask SetPeerClient(IClient client);
    ValueTask ShowPeerVideo(IMediaStream stream);
    ValueTask ShowSelfVideo(IMediaStream stream);
}


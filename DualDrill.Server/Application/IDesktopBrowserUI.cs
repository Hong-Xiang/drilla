using DualDrill.Engine.Connection;
using DualDrill.Engine.WebRTC;
using Microsoft.JSInterop;

namespace DualDrill.Server.Application;

internal interface IDesktopBrowserUI
{
    ValueTask<IJSObjectReference> GetCanvasElement();
    ValueTask SetPeerClient(IClient client, RTCPeerConnectionPair pair);
    ValueTask RemovePeerClient();

    ValueTask ShowPeerVideo(IMediaStream stream);
    ValueTask ShowSelfVideo(IMediaStream stream);

    ValueTask ClosePeerVideo();
    ValueTask CloseSelfVideo();
}


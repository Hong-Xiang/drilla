using DualDrill.Server.BrowserClient;

namespace DualDrill.Server.Application;

interface IPeerVideoUI
{
    Task ShowPeerVideo(JSMediaStreamProxy mediaStream);
    Task ShowSelfVideo(JSMediaStreamProxy mediaStream);
}

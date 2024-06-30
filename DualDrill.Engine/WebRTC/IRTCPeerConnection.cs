using DualDrill.Engine.BrowserProxy;
using DualDrill.Engine.Connection;
using DualDrill.Engine.UI;
using Microsoft.JSInterop;
using System.Reactive;

namespace DualDrill.Engine.WebRTC;

public interface IRTCPeerConnection : IAsyncDisposable
{
    IClient Client { get; }
    ValueTask<string> CreateOffer();
    ValueTask<string> CreateAnswer();
    ValueTask SetLocalDescription(RTCSessionDescription type, string sdp);
    ValueTask SetRemoteDescription(RTCSessionDescription type, string sdp);
    ValueTask AddIceCandidate(object iceCandidate);
    ValueTask<JSDisposableProxy> WaitVideoStream(string id, TaskCompletionSourceDotnetObjectReference<IJSObjectReference> tcs);
    Task AddVideoStream(IJSObjectReference stream);
    IObservable<Unit> NegotiationNeeded { get; }
    IObservable<string> ConnectionStateChange { get; }
    IObservable<object> IceCandidate { get; }
}

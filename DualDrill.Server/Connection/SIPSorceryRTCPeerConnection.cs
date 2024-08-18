using DualDrill.Engine.BrowserProxy;
using DualDrill.Engine.Connection;
using DualDrill.Engine.Media;
using Microsoft.JSInterop;
using R3;
using SIPSorcery.Net;

namespace DualDrill.Server.Services;

sealed class SIPSorceryRTCPeerConnection(
    RTCPeerConnection Connection
) : IPeerConnection
{
    public IClient Client => throw new NotImplementedException();

    public IObservable<System.Reactive.Unit> NegotiationNeeded => throw new NotImplementedException();

    public IObservable<string> ConnectionStateChange => throw new NotImplementedException();

    public IObservable<object> IceCandidate => throw new NotImplementedException();

    public Guid SelfId => throw new NotImplementedException();

    public Guid PeerId => throw new NotImplementedException();

    public Observable<IDataChannel> OnDataChannel => throw new NotImplementedException();

    public Observable<IMediaStreamTrack> OnTrack => throw new NotImplementedException();

    public Observable<RTCPeerConnectionState> OnConnectionStateChange => throw new NotImplementedException();

    public ValueTask AddIceCandidate(object iceCandidate)
    {
        throw new NotImplementedException();
    }

    public ValueTask AddTrack(IMediaStreamTrack track)
    {
        throw new NotImplementedException();
    }

    public Task AddVideoStream(IJSObjectReference stream)
    {
        throw new NotImplementedException();
    }

    public ValueTask<string> CreateAnswer()
    {
        throw new NotImplementedException();
    }

    public ValueTask<IDataChannel> CreateDataChannel(string label)
    {
        throw new NotImplementedException();
    }

    public ValueTask<string> CreateOffer()
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public ValueTask DisposeAsync()
    {
        throw new NotImplementedException();
    }

    public ValueTask SetLocalDescription(Engine.WebRTC.RTCSessionDescription type, string sdp)
    {
        throw new NotImplementedException();
    }

    public ValueTask SetRemoteDescription(Engine.WebRTC.RTCSessionDescription type, string sdp)
    {
        throw new NotImplementedException();
    }

    public ValueTask StartAsync(CancellationToken cancellation)
    {
        throw new NotImplementedException();
    }

    public ValueTask<JSDisposableProxy> WaitVideoStream(string id, TaskCompletionSourceDotnetObjectReference<IJSObjectReference> tcs)
    {
        throw new NotImplementedException();
    }
}

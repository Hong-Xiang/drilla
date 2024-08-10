using DualDrill.Common.ResourceManagement;
using DualDrill.Engine.Connection;
using DualDrill.Engine.WebRTC;
using Microsoft.JSInterop;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace DualDrill.Engine.BrowserProxy;

public sealed class RTCPeerConnectionProxy(
    IClient Client,
    IJSObjectReference Reference,
    IObservable<string> ConnectionStateChange,
    IObservable<object> IceCandidate,
    IObservable<Unit> NegotiationNeeded,
    IAsyncDisposable Done) : IRTCPeerConnection, IAsyncDisposable
{
    public IClient Client { get; } = Client;

    public IObservable<Unit> NegotiationNeeded { get; } = NegotiationNeeded;

    public IObservable<object> IceCandidate { get; } = IceCandidate;

    public IObservable<string> ConnectionStateChange { get; } = ConnectionStateChange;

    static async Task<JSDisposableProxy> SubscribeAsync<T>(
        IClient client,
        IJSObjectReference jsProxy,
        DotNetObjectReference<SubjectReferenceWrapper<T>> subject,
        string type)
    {
        var sub = await jsProxy.InvokeAsync<IJSObjectReference>("subscribe", subject, type);
        return new(client, sub);
    }


    public static ValueTask<RTCPeerConnectionProxy> CreateAsync(
             IClient client,
             JSClientModule module)
    {

        return AsyncResource2.CreateAsync<RTCPeerConnectionProxy>(async (builder) =>
        {
            await using var jsConnection = await module.CreateRtcPeerConnectionAsync();

            using var stateChangeSubject = new SubjectReferenceWrapper<string>(new Subject<string>());
            await using var stateChangeSub = await SubscribeAsync(client, jsConnection, stateChangeSubject.Reference, "connectionstatechange");

            using var iceCandidateSubject = new SubjectReferenceWrapper<object>(new Subject<object>());
            await using var iceCandidateSub = await SubscribeAsync(client, jsConnection, iceCandidateSubject.Reference, "icecandidate");

            using var negotiationNeededSubject = new SubjectReferenceWrapper<int>(new Subject<int>());
            await using var negotiationNeededSub = await SubscribeAsync(client, jsConnection, negotiationNeededSubject.Reference, "negotiationneeded");


            builder.SetResult(new(
                client,
                jsConnection,
                stateChangeSubject.Observable,
                iceCandidateSubject.Observable,
                negotiationNeededSubject.Observable.Select(x => default(Unit)),
                builder));
            await builder.Done().ConfigureAwait(false);
        });
    }


    public async ValueTask AddIceCandidate(object iceCandidate)
    {
        await Reference.InvokeVoidAsync("addIceCandidate", iceCandidate).ConfigureAwait(false);
    }

    public async ValueTask<string> CreateAnswer()
    {
        return await Reference.InvokeAsync<string>("createAnswer").ConfigureAwait(false);
    }

    public async ValueTask<string> CreateOffer()
    {
        return await Reference.InvokeAsync<string>("createOffer").ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await Done.DisposeAsync().ConfigureAwait(false);
    }

    private static string RTCSessionDescriptionString(RTCSessionDescription type)
    {
        return type switch
        {
            RTCSessionDescription.Answer => "answer",
            RTCSessionDescription.Offer => "offer",
            _ => throw new NotImplementedException()
        };
    }


    public async ValueTask SetLocalDescription(RTCSessionDescription type, string sdp)
    {
        await Reference.InvokeAsync<string>("setLocalDescription", RTCSessionDescriptionString(type), sdp).ConfigureAwait(false);
    }

    public async ValueTask SetRemoteDescription(RTCSessionDescription type, string sdp)
    {
        await Reference.InvokeAsync<string>("setRemoteDescription", RTCSessionDescriptionString(type), sdp).ConfigureAwait(false);
    }

    //public async ValueTask<(JSDisposableProxy, Task<IMediaStream>)> WaitVideoStream(string id)
    //{
    //    static async Task<IMediaStream> GetMediaStreamProxy(Task<IJSObjectReference> response, IClient client, string id)
    //    {
    //        var module = client.Services.GetRequiredService<JSClientModule>();
    //        var stream = await response.ConfigureAwait(false);
    //        return new JSMediaStreamProxy(client, module, stream, id);
    //    }
    //    var tcs = new TaskCompletionSourceDotnetObjectReference<IJSObjectReference>();
    //    var stream = GetMediaStreamProxy(tcs.Task, Client, id);
    //    var sub = await Reference.InvokeAsync<IJSObjectReference>("waitVideoStream", id, tcs.Reference).ConfigureAwait(false);
    //    return (new(Client, sub), stream);
    //}

    public async ValueTask<JSDisposableProxy> WaitVideoStream(string id, TaskCompletionSourceDotnetObjectReference<IJSObjectReference> tcs)
    {
        var sub = await Reference.InvokeAsync<IJSObjectReference>("waitVideoStream", id, tcs.Reference).ConfigureAwait(false);
        return new(Client, sub);
    }

    public async Task AddVideoStream(IJSObjectReference stream)
    {
        await Reference.InvokeVoidAsync("addVideoStream", stream).ConfigureAwait(false);
    }
}

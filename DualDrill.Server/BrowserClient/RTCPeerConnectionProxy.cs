using DualDrill.Common.ResourceManagement;
using DualDrill.Engine.Connection;
using Microsoft.JSInterop;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Channels;

namespace DualDrill.Server.BrowserClient;


sealed class RTCPeerConnectionProxy(IJSObjectReference JS,
    IObservable<string> ConnectionStateChange,
    IObservable<object> IceCandidate,
    IObservable<Unit> NegotiationNeeded,
    IAsyncDisposable Resource) : IAsyncDisposable
{
    public IObservable<string> ConnectionStateChange { get; } = ConnectionStateChange;
    public IObservable<object> IceCandidate { get; } = IceCandidate;
    public IObservable<Unit> NegotiationNeeded { get; } = NegotiationNeeded;

    public async Task<string> GetConnectionState()
    {
        return await JS.InvokeAsync<string>("getConnectionState").ConfigureAwait(false);
    }

    public async Task<string> CreateOffer()
    {
        return await JS.InvokeAsync<string>("createOffer").ConfigureAwait(false);
    }
    public async Task<string> SetOffer(string offer)
    {
        return await JS.InvokeAsync<string>("setOffer", offer).ConfigureAwait(false);
    }
    public async Task SetAnswer(string sdp)
    {
        await JS.InvokeVoidAsync("setAnswer", sdp).ConfigureAwait(false);
    }
    public async Task AddIceCandidate(object candidate)
    {
        await JS.InvokeVoidAsync("addIceCandidate", candidate);
    }

    sealed class EventObserver() : IDisposable
    {
        internal Subject<string> ConnectionStateChangeSubject = new();
        internal Subject<object> IceCandidateSubject = new();
        internal Subject<Unit> NegotiationNeededSubject = new();


        public void Dispose()
        {
            ConnectionStateChangeSubject.Dispose();
            NegotiationNeededSubject.Dispose();
            IceCandidateSubject.Dispose();
        }

        [JSInvokable]
        public void OnConnectionStateChange(string state)
        {
            ConnectionStateChangeSubject.OnNext(state);
        }

        [JSInvokable]
        public void OnIceCandidate(object candidate)
        {
            IceCandidateSubject.OnNext(candidate);
        }

        [JSInvokable]
        public void OnNegotiationNeeded()
        {
            NegotiationNeededSubject.OnNext(default);
        }
    }

    public async Task<JSUnmanagedResourceReference> WaitDataChannelAsync(string label,
        DotNetObjectReference<TaskCompletionSourceJSWrapper<IJSObjectReference>> tcs)
    {
        var sub = await JS.InvokeAsync<IJSObjectReference>("waitDataChannel", label, tcs).ConfigureAwait(false);
        return new(sub);
    }
    public async Task<RTCDataChannelProxy> CreateDataChannelAsync(string label)
    {
        var channel = await JS.InvokeAsync<IJSObjectReference>("createDataChannel", label).ConfigureAwait(false);
        return new RTCDataChannelProxy(channel);
    }




    static async Task<JSUnmanagedResourceReference> SubscribeAsync<T>(
        IJSObjectReference jsProxy, DotNetObjectReference<SubjectJSWrapper<T>> subject, string type)
    {
        var sub = await jsProxy.InvokeAsync<IJSObjectReference>("subscribe", subject, type);
        return new JSUnmanagedResourceReference(sub);
    }

    static async IAsyncEnumerable<Func<IAsyncDisposable, RTCPeerConnectionProxy>> CreateInternalAsync(ClientModule clientModule)
    {
        await using var jsConnection = await clientModule.CreateRtcPeerConnection();
        using var observer = new EventObserver();
        using var observerReference = DotNetObjectReference.Create(observer);

        using var stateChangeSubject = new SubjectJSWrapper<string>(new Subject<string>());
        using var stateChangeSubjectReference = DotNetObjectReference.Create(stateChangeSubject);
        await using var stateChangeSub = await SubscribeAsync(jsConnection.Value, stateChangeSubjectReference, "connectionstatechange");

        using var iceCandidateSubject = new SubjectJSWrapper<object>(new Subject<object>());
        using var iceCandidateSubjectReference = DotNetObjectReference.Create(iceCandidateSubject);
        await using var iceCandidateSub = await SubscribeAsync(jsConnection.Value, iceCandidateSubjectReference, "icecandidate");

        using var negotiationNeededSubject = new SubjectJSWrapper<int>(new Subject<int>());
        using var negotiationNeededSubjectReference = DotNetObjectReference.Create(negotiationNeededSubject);
        await using var negotiationNeededSub = await SubscribeAsync(jsConnection.Value, negotiationNeededSubjectReference, "negotiationneeded");


        yield return (dispose) => new(jsConnection.Value, stateChangeSubject.Observable, iceCandidateSubject.Observable, negotiationNeededSubject.Observable.Select(x => default(Unit)), dispose);
        Console.WriteLine("Dispose called");
    }

    public static async Task<RTCPeerConnectionProxy> CreateAsync(ClientModule clientModule)
    {
        return await AsyncResource.CreateAsync(CreateInternalAsync(clientModule)).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        Console.WriteLine("RTCPeer Dispose called");
        await Resource.DisposeAsync().ConfigureAwait(false);
    }
}

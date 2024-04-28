using DualDrill.Common.ResourceManagement;
using DualDrill.Engine.BrowserProxy;
using DualDrill.Engine.Connection;
using Microsoft.JSInterop;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Channels;

namespace DualDrill.Server.BrowserClient;


//sealed class RTCPeerConnectionProxy(
//    IBrowserClient Client,
//    IJSObjectReference JS,
//    IObservable<string> ConnectionStateChange,
//    IObservable<object> IceCandidate,
//    IObservable<Unit> NegotiationNeeded,
//    IAsyncDisposable Resource) : IAsyncDisposable
//{
//    public IBrowserClient Client { get; } = Client;
//    public IObservable<string> ConnectionStateChange { get; } = ConnectionStateChange;
//    public IObservable<object> IceCandidate { get; } = IceCandidate;
//    public IObservable<Unit> NegotiationNeeded { get; } = NegotiationNeeded;

//    public async Task<string> GetConnectionState()
//    {
//        return await JS.InvokeAsync<string>("getConnectionState").ConfigureAwait(false);
//    }

//    public async Task<string> CreateOffer()
//    {
//        return await JS.InvokeAsync<string>("createOffer").ConfigureAwait(false);
//    }
//    public async Task<string> SetOffer(string offer)
//    {
//        return await JS.InvokeAsync<string>("setOffer", offer).ConfigureAwait(false);
//    }
//    public async Task SetAnswer(string sdp)
//    {
//        await JS.InvokeVoidAsync("setAnswer", sdp).ConfigureAwait(false);
//    }
//    public async Task AddIceCandidate(object candidate)
//    {
//        await JS.InvokeVoidAsync("addIceCandidate", candidate);
//    }

//    sealed class EventObserver() : IDisposable
//    {
//        internal Subject<string> ConnectionStateChangeSubject = new();
//        internal Subject<object> IceCandidateSubject = new();
//        internal Subject<Unit> NegotiationNeededSubject = new();


//        public void Dispose()
//        {
//            ConnectionStateChangeSubject.Dispose();
//            NegotiationNeededSubject.Dispose();
//            IceCandidateSubject.Dispose();
//        }

//        [JSInvokable]
//        public void OnConnectionStateChange(string state)
//        {
//            ConnectionStateChangeSubject.OnNext(state);
//        }

//        [JSInvokable]
//        public void OnIceCandidate(object candidate)
//        {
//            IceCandidateSubject.OnNext(candidate);
//        }

//        [JSInvokable]
//        public void OnNegotiationNeeded()
//        {
//            NegotiationNeededSubject.OnNext(default);
//        }
//    }

//    public async Task<JSDisposableReference> WaitDataChannelAsync(string label,
//        DotNetObjectReference<TaskCompletionSourceReferenceWrapper<IJSObjectReference>> tcs)
//    {
//        var sub = await JS.InvokeAsync<IJSObjectReference>("waitDataChannel", label, tcs).ConfigureAwait(false);
//        return new(sub);
//    }
//    public async Task<RTCDataChannelProxy> CreateDataChannelAsync(string label)
//    {
//        var channel = await JS.InvokeAsync<IJSObjectReference>("createDataChannel", label).ConfigureAwait(false);
//        return new RTCDataChannelProxy(Client, channel);
//    }

//    public async Task<JSDisposableReference> WaitVideoStream(string id,
//        DotNetObjectReference<PrimitiveJSPromiseBuilder<IJSObjectReference>> tcs)
//    {
//        var sub = await JS.InvokeAsync<IJSObjectReference>("waitVideoStream", id, tcs).ConfigureAwait(false);
//        return new(sub);
//    }
//    public async Task AddVideoStream(IJSObjectReference stream)
//    {
//        await JS.InvokeVoidAsync("addVideoStream", stream).ConfigureAwait(false);
//    }

//    static async Task<JSDisposableReference> SubscribeAsync<T>(
//        IJSObjectReference jsProxy, DotNetObjectReference<SubjectReferenceWrapper<T>> subject, string type)
//    {
//        var sub = await jsProxy.InvokeAsync<IJSObjectReference>("subscribe", subject, type);
//        return new JSDisposableReference(sub);
//    }

//    static async IAsyncEnumerable<Func<IAsyncDisposable, RTCPeerConnectionProxy>> CreateInternalAsync(
//        IBrowserClient client)
//    {
//        await using var jsConnection = await client.Module.CreateRtcPeerConnection();
//        using var observer = new EventObserver();
//        using var observerReference = DotNetObjectReference.Create(observer);

//        using var stateChangeSubject = new SubjectReferenceWrapper<string>(new Subject<string>());
//        using var stateChangeSubjectReference = DotNetObjectReference.Create(stateChangeSubject);
//        await using var stateChangeSub = await SubscribeAsync(jsConnection.Value, stateChangeSubjectReference, "connectionstatechange");

//        using var iceCandidateSubject = new SubjectReferenceWrapper<object>(new Subject<object>());
//        using var iceCandidateSubjectReference = DotNetObjectReference.Create(iceCandidateSubject);
//        await using var iceCandidateSub = await SubscribeAsync(jsConnection.Value, iceCandidateSubjectReference, "icecandidate");

//        using var negotiationNeededSubject = new SubjectReferenceWrapper<int>(new Subject<int>());
//        using var negotiationNeededSubjectReference = DotNetObjectReference.Create(negotiationNeededSubject);
//        await using var negotiationNeededSub = await SubscribeAsync(jsConnection.Value, negotiationNeededSubjectReference, "negotiationneeded");

//        yield return (dispose) => new(client, jsConnection.Value, stateChangeSubject.Observable, iceCandidateSubject.Observable, negotiationNeededSubject.Observable.Select(x => default(Unit)), dispose);
//    }

//    public static async Task<RTCPeerConnectionProxy> CreateAsync(IBrowserClient client)
//    {
//        return await AsyncResource.CreateAsync(CreateInternalAsync(client)).ConfigureAwait(false);
//    }

//    public async ValueTask DisposeAsync()
//    {
//        await Resource.DisposeAsync().ConfigureAwait(false);
//    }
//}

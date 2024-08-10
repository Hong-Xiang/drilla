using DualDrill.Engine.Connection;
using DualDrill.Engine.Input;
using DualDrill.Engine.Media;
using MessagePipe;
using R3;
using SIPSorcery.Net;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
namespace DualDrill.Server.Services;

sealed class SIPSorceryDataChannel : IDataChannel
{
    private Subject<ReadOnlyMemory<byte>> OnMessageSubject = new();
    private RTCDataChannel DataChannel { get; }
    public string Label => DataChannel.label;
    public int Id => (int)DataChannel.id;

    public SIPSorceryDataChannel(RTCDataChannel dataChannel)
    {
        DataChannel = dataChannel;
        DataChannel.onmessage += OnDataChannelMessage;
        OnMessage = OnMessageSubject.AsObservable();
    }

    public void Send(ReadOnlySpan<byte> data)
    {
        DataChannel.send(data.ToArray());
    }
    public Observable<ReadOnlyMemory<byte>> OnMessage { get; }

    public void Dispose()
    {
        DataChannel.onmessage -= OnDataChannelMessage;
        OnMessageSubject.Dispose();
    }
    private void OnDataChannelMessage(RTCDataChannel dc, DataChannelPayloadProtocols protocol, byte[] data)
    {
        OnMessageSubject.OnNext(data);
    }
}

//sealed class SIPSorceryRTCPeerConnection : IPeerConnection
//{
//    private CompositeDisposable Disposables { get; }
//    public SIPSorceryRTCPeerConnection(
//        ISignalConnection signalConnection,
//        EventFactory eventFactory,
//        ILogger<SIPSorceryRTCPeerConnection> logger
//    )
//    {
//        Disposables = new();

//        SignalConnection = signalConnection;
//        EventFactory = eventFactory;
//        Logger = logger;

//        Connection = new RTCPeerConnection(new RTCConfiguration
//        {
//            iceServers = new List<RTCIceServer>()
//        });
//        Disposables.Add(Connection);

//        Disposables.Add(
//            SignalConnection.OnOffer.SubscribeAwait(async (offer, cancellation) =>
//            {
//                await OnNegotiationAsync(offer, cancellation);
//            })
//        );

//        Disposables.Add(
//            Observable.FromEvent(
//                h => { Connection.onnegotiationneeded += h; },
//                h => { Connection.onnegotiationneeded -= h; }
//            ).SubscribeAwait(async (_, cancellation) =>
//            {
//                await NegotiationAsync(cancellation);
//            }));

//        Disposables.Add(
//            Observable.FromEvent<RTCIceConnectionState>(
//                h => { Connection.oniceconnectionstatechange += h; },
//                h => { Connection.oniceconnectionstatechange -= h; }
//            ).Subscribe(state =>
//            {
//                logger.LogInformation("ice candidate state changed to {state}", Enum.GetName(state));
//            }));

//        Connection.onicecandidateerror += (err, msg) =>
//        {
//            logger.LogError(msg + Environment.NewLine + JsonSerializer.Serialize(err));
//        };


//        var (onIceCandidateEmit, onIceCandidate) = EventFactory.CreateAsyncEvent<RTCIceCandidate>();
//        Connection.onicecandidate += (c) => onIceCandidateEmit.Publish(c);
//        Disposables.Add(onIceCandidate.Subscribe(async (candidate, cancellation) =>
//        {
//            await SignalConnection.AddIceCandidateAsync(JsonSerializer.Serialize(candidate), cancellation);
//        }));

//        SignalConnection.OnIceCandidate.SubscribeAwait(async (candidate, cancellation) =>
//        {
//            Connection.addIceCandidate(JsonSerializer.Deserialize<RTCIceCandidateInit>(candidate));
//        });


//        OnConnectionStateChange = Observable.FromEvent<RTCPeerConnectionState>(h =>
//           {
//               Connection.onconnectionstatechange += h;
//           }, h =>
//           {
//               Connection.onconnectionstatechange -= h;
//           });

//        Disposables.Add(OnConnectionStateChange.Subscribe((state) =>
//        {
//            Logger.LogInformation("Peer connection state change to {state}.", state);
//            switch (state)
//            {
//                case RTCPeerConnectionState.connected:
//                    VideoSource.OnVideoSourceEncodedSample += sendVideo;
//                    Logger.LogInformation("{ConnectionId} RTC Connected", connectionId);
//                    break;
//                case RTCPeerConnectionState.failed:
//                    Connection.Close("ice disconnection");
//                    break;
//                case RTCPeerConnectionState.closed:
//                    VideoSource.OnVideoSourceEncodedSample -= sendVideo;
//                    Logger.LogInformation("{ConnectionId} RTC Disconnected", connectionId);
//                    break;
//            }
//        }));


//        OnDataChannel = Observable.FromEvent<RTCDataChannel>(
//            h => { Connection.ondatachannel += h; },
//            h => { Connection.ondatachannel -= h; }
//        ).Select(channel => new SIPSorceryDataChannel(channel) as IDataChannel);

//        Connection.ondatachannel += ConnectionOnDataChannel;
//    }

//    void SendVideo(MediaStreamTrack track, uint duration, ReadOnlySpan<byte> buffer)
//    {
//        if (Connection.connectionState == RTCPeerConnectionState.connected)
//        {
//            var s = Connection.VideoStreamList.FirstOrDefault(s => s.LocalTrack == track);
//            if (s is null)
//            {
//                Connection.addTrack(track);
//                s = Connection.VideoStreamList.First(s => s.LocalTrack == track);
//            }
//            s.SendVideo(duration, buffer.ToArray());
//        }
//    }

//    private async ValueTask NegotiationAsync(CancellationToken cancellation)
//    {
//        var offer = Connection.createOffer();
//        await Connection.setLocalDescription(offer);
//        var answerSdp = await SignalConnection.NegotiateAsync(offer.sdp, cancellation);
//        var result = Connection.setRemoteDescription(new RTCSessionDescriptionInit
//        {
//            type = RTCSdpType.answer,
//            sdp = answerSdp,
//        });
//        if (result != SetDescriptionResultEnum.OK)
//        {
//            throw new InvalidOperationException($"Failed to set remote description {Enum.GetName(result)}");
//        }
//    }
//    private async ValueTask<string> OnNegotiationAsync(string offer, CancellationToken cancellation)
//    {
//        Connection.setRemoteDescription(new RTCSessionDescriptionInit
//        {
//            type = RTCSdpType.offer,
//            sdp = offer,
//        });
//        var answer = Connection.createAnswer();
//        await Connection.setLocalDescription(answer);
//        return answer.sdp;
//    }

//    private void ConnectionOnDataChannel(RTCDataChannel dataChannel)
//    {

//        if (dataChannel.label == "pointermove")
//        {
//            dataChannel.onmessage += (dc, protocol, data) =>
//            {
//                Logger.LogInformation(Encoding.UTF8.GetString(data));
//            };
//        }
//    }

//    private RTCPeerConnection Connection { get; }

//    public async ValueTask<IDataChannel> CreateDataChannel(string label)
//    {
//        var dc = await Connection.createDataChannel(label);
//        return new SIPSorceryDataChannel(dc);
//    }

//    public Observable<IDataChannel> OnDataChannel { get; }
//    private ISignalConnection SignalConnection { get; }
//    private EventFactory EventFactory { get; }
//    public ILogger<SIPSorceryRTCPeerConnection> Logger { get; }

//    public Observable<RTCPeerConnectionState> OnConnectionStateChange { get; }

//    public void Dispose()
//    {
//        Connection.ondatachannel -= ConnectionOnDataChannel;
//        Disposables.Dispose();
//    }

//    public async ValueTask StartAsync(CancellationToken cancellation)
//    {
//        var connected = new TaskCompletionSource();
//        void OnStateChange(RTCPeerConnectionState state)
//        {
//            Connection.onconnectionstatechange -= OnStateChange;
//            connected.SetResult();
//        }
//        Connection.onconnectionstatechange += OnStateChange;
//        await NegotiationAsync(cancellation);
//        await connected.Task;
//    }
//}


sealed partial class RTCPeerConnectionProviderService(
    ILogger<RTCPeerConnectionProviderService> Logger,
    ClientStore ClientStore,
    HeadlessSurfaceCaptureVideoSource VideoSource,
    IPublisher<PairIdentity> CreatePairPublisher,
    IPublisher<ClientInput<PointerEvent>> PointerEventPublisher,
    ISignalConnectionProviderService SignalConnectionService)
{
    ConcurrentDictionary<Guid, RTCPeerConnection> Connections = [];
    public RTCPeerConnection CreatePeerConnection(Guid clientId)
    {
        var disposables = new CompositeDisposable();
        var sendId = new PairIdentity(ClientStore.ServerId, clientId);
        var recvId = new PairIdentity(clientId, ClientStore.ServerId);

        var pc = new RTCPeerConnection(new RTCConfiguration
        {
            iceServers = []
        });

        disposables.Add(
            Observable.FromEvent(
                h => pc.onnegotiationneeded += h,
                h => pc.onnegotiationneeded -= h
            ).SubscribeAwait(
                async (_, cancellation) =>
                {
                    var offer = pc.createOffer();
                    await pc.setLocalDescription(offer);
                    await SignalConnectionService.OfferAsync(ClientStore.ServerId, clientId, offer.sdp);
                }
            )

        );

        disposables.Add(
            SignalConnectionService.SubscribeOfferAwait(clientId, ClientStore.ServerId, async (sdp, cancellation) =>
            {
                pc.setRemoteDescription(new RTCSessionDescriptionInit
                {
                    type = RTCSdpType.offer,
                    sdp = sdp
                });
                var answer = pc.createAnswer();
                await pc.setLocalDescription(answer);
                await SignalConnectionService.AnswerAsync(ClientStore.ServerId, clientId, answer.sdp);
            })
        );

        disposables.Add(
            SignalConnectionService.SubscribeAnswerAwait(clientId, ClientStore.ServerId, async (sdp, cancellation) =>
            {
                pc.setRemoteDescription(new RTCSessionDescriptionInit
                {
                    type = RTCSdpType.answer,
                    sdp = sdp
                });
            })
        );

        disposables.Add(
            Observable.FromEvent<RTCIceCandidate>(
                h => pc.onicecandidate += h,
                h => pc.onicecandidate -= h
            ).SubscribeAwait(
                async (candidate, cancellation) =>
                {
                    await SignalConnectionService.AddIceCandidateAsync(ClientStore.ServerId, clientId, JsonSerializer.Serialize(candidate));
                })
        );

        disposables.Add(
            SignalConnectionService.SubscribeAddIceCandidateAwait(clientId, ClientStore.ServerId,
            async (data, cancellation) =>
            {
                var candidate = JsonSerializer.Deserialize<RTCIceCandidateInit>(data);
                pc.addIceCandidate(candidate);
            })
        );

        disposables.Add(
            Observable.FromEvent<RTCIceConnectionState>(
                h => pc.oniceconnectionstatechange += h,
                h => pc.oniceconnectionstatechange -= h
            ).Subscribe(Logger.LogIceStateChanged));
        pc.onicecandidateerror += (err, msg) =>
        {
            Logger.LogError(msg + Environment.NewLine + JsonSerializer.Serialize(err));
        };

        var track = new MediaStreamTrack(VideoSource.VideoEncoder.SupportedFormats, MediaStreamStatusEnum.SendOnly);
        pc.addTrack(track);

        void sendVideo(uint duration, VideoFrameBuffer buffer)
        {
            if (pc.connectionState == RTCPeerConnectionState.connected)
            {
                var s = pc.VideoStreamList.First(s => s.LocalTrack == track);
                s.SendVideo(duration, buffer.Memory.ToArray());
            }
        }

        disposables.Add(
            Observable.FromEvent<RTCDataChannel>(
                h => pc.ondatachannel += h,
                h => pc.ondatachannel -= h
            ).Subscribe(channel =>
            {
                if (channel.label == "pointermove")
                {
                    channel.onmessage += (dc, protocol, data) =>
                    {
                        var e = JsonSerializer.Deserialize<PointerEvent>(data, JsonSerializerOptions.Web);
                        if (e is not null)
                        {
                            PointerEventPublisher.Publish(new ClientInput<PointerEvent>(clientId, e));
                        }
                        else
                        {
                            Logger.LogWarning("Failed to deserialize pointer event {data}", Encoding.UTF8.GetString(data));
                        }
                    };
                }
            }));

        disposables.Add(
            Observable.FromEvent<RTCPeerConnectionState>(
                h => pc.onconnectionstatechange += h,
                h => pc.onconnectionstatechange -= h
            ).Subscribe(state =>
            {
                Logger.LogInformation("Peer connection from {clientId} state change to {state}.", clientId, state);

                switch (state)
                {
                    case RTCPeerConnectionState.connected:
                        VideoSource.OnVideoSourceEncodedSample += sendVideo;
                        Logger.LogInformation("{ClientId} RTC Connected", clientId);
                        break;
                    case RTCPeerConnectionState.failed:
                        pc.Close("ice disconnection");
                        break;
                    case RTCPeerConnectionState.closed:
                        VideoSource.OnVideoSourceEncodedSample -= sendVideo;
                        Logger.LogInformation("{ClientId} RTC Disconnected", clientId);
                        Connections.TryRemove(clientId, out _);
                        break;
                }
            }));

        CreatePairPublisher.Publish(sendId);

        if (!Connections.TryAdd(clientId, pc))
        {
            Logger.LogError("Failed to store connection {ClientId}", clientId);
        };
        return pc;
    }
}

static partial class LogExtension
{
    [LoggerMessage(Level = LogLevel.Information, Message = "ICE candidate state changed to {state}")]
    public static partial void LogIceStateChanged(this ILogger<RTCPeerConnectionProviderService> logger, RTCIceConnectionState state);
}

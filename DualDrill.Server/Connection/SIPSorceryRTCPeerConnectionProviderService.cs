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

public sealed partial class SIPSorceryRTCPeerConnectionProviderService(
    ILogger<SIPSorceryRTCPeerConnectionProviderService> Logger,
    HeadlessSurfaceCaptureVideoSource VideoSource,
    IPublisher<PairIdentity> CreatePairPublisher,
    IPublisher<ClientEvent<PointerEvent>> PointerEventPublisher,
    IPublisher<ClientEvent<ScaleEvent>> ScaleEventPublisher,
    ISignalConnectionProviderService SignalConnectionService)
    : IPeerConnectionProviderService
{
    ConcurrentDictionary<Guid, RTCPeerConnection> Connections = [];
    public DualDrill.Engine.WebRTC.IRTCPeerConnection CreatePeerConnection(Guid clientId)
    {
        SignalConnectionService.CreateConnection(ClientConnectionManagerService.ServerId, clientId);
        var disposables = new CompositeDisposable();
        var sendId = new PairIdentity(ClientConnectionManagerService.ServerId, clientId);
        var recvId = new PairIdentity(clientId, ClientConnectionManagerService.ServerId);

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
                    await SignalConnectionService.OfferAsync(ClientConnectionManagerService.ServerId, clientId, offer.sdp);
                }
            ));

        disposables.Add(
            SignalConnectionService.SubscribeOfferAwait(clientId, ClientConnectionManagerService.ServerId, async (sdp, cancellation) =>
            {
                pc.setRemoteDescription(new RTCSessionDescriptionInit
                {
                    type = RTCSdpType.offer,
                    sdp = sdp
                });
                var answer = pc.createAnswer();
                await pc.setLocalDescription(answer);
                await SignalConnectionService.AnswerAsync(ClientConnectionManagerService.ServerId, clientId, answer.sdp);
            })
        );

        disposables.Add(
            SignalConnectionService.SubscribeAnswerAwait(clientId, ClientConnectionManagerService.ServerId, async (sdp, cancellation) =>
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
                    await SignalConnectionService.AddIceCandidateAsync(ClientConnectionManagerService.ServerId, clientId, JsonSerializer.Serialize(candidate));
                })
        );

        disposables.Add(
            SignalConnectionService.SubscribeAddIceCandidateAwait(clientId, ClientConnectionManagerService.ServerId,
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
                            PointerEventPublisher.Publish(new ClientEvent<PointerEvent>(clientId, e));
                        }
                        else
                        {
                            Logger.LogWarning("Failed to deserialize pointer event {data}", Encoding.UTF8.GetString(data));
                        }
                    };
                }
                if (channel.label == "scale")
                {
                    channel.onmessage += (dc, protocol, data) =>
                    {
                        var e = JsonSerializer.Deserialize<ScaleEvent>(data, JsonSerializerOptions.Web);
                        if (e is not null)
                        {
                            ScaleEventPublisher.Publish(new ClientEvent<ScaleEvent>(clientId, e));
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
        return new SIPSorceryRTCPeerConnection(pc);
    }
}

static partial class LogExtension
{
    [LoggerMessage(Level = LogLevel.Information, Message = "ICE candidate state changed to {state}")]
    public static partial void LogIceStateChanged(this ILogger<SIPSorceryRTCPeerConnectionProviderService> logger, RTCIceConnectionState state);
}

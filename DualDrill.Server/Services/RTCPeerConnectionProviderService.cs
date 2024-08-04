using DualDrill.Engine.Media;
using FFmpeg.AutoGen;
using MessagePipe;
using Microsoft.AspNetCore.SignalR;
using SIPSorcery.Net;
using System.Text;
using System.Text.Json;
namespace DualDrill.Server.Services;

sealed class RTCPeerConnectionProviderService(
    HeadlessSurfaceCaptureVideoSource VideoSource,
    ILogger<RTCPeerConnectionProviderService> Logger,
    IHubContext<DrillHub, IDrillHubClient> hubContext,
    EventFactory EventFactory)
{
    public RTCPeerConnection CreatePeerConnection(string connectionId)
    {
        var client = hubContext.Clients.Client(connectionId);
        var pc = new RTCPeerConnection(new RTCConfiguration
        {
            iceServers = new List<RTCIceServer>()
        });
        var track = new MediaStreamTrack(VideoSource.VideoEncoder.SupportedFormats, MediaStreamStatusEnum.SendOnly);
        pc.addTrack(track);
        pc.ondatachannel += (channel) =>
                 {
                     if (channel.label == "pointermove")
                     {
                         channel.onmessage += (dc, protocol, data) =>
                         {
                             Logger.LogInformation(Encoding.UTF8.GetString(data));
                         };
                     }
                 };
        pc.oniceconnectionstatechange += (state) =>
        {
            Logger.LogInformation("ice candidate state changed to {state}", Enum.GetName(state));
        };
        var (negotiateNeededEmitter, onNegotiateNeeded) = EventFactory.CreateAsyncEvent<int>();
        onNegotiateNeeded.Subscribe(async (_, cancel) =>
        {
            await client.RequestNegotiation();
        });
        pc.onicecandidateerror += (err, msg) =>
        {
            Logger.LogError(msg + Environment.NewLine + JsonSerializer.Serialize(err));
        };
        pc.onnegotiationneeded += () =>
        {
            negotiateNeededEmitter.Publish(0);
            Logger.LogWarning("Negotiation Needed");
        };
        pc.onicecandidate += (c) =>
        {
            client.IceCandidate(JsonSerializer.Serialize(c));
        };

        void sendVideo(uint duration, VideoFrameBuffer buffer)
        {
            if (pc.connectionState == RTCPeerConnectionState.connected)
            {
                var s = pc.VideoStreamList.First(s => s.LocalTrack == track);
                s.SendVideo(duration, buffer.Memory.ToArray());
            }
        }
        pc.onconnectionstatechange += (state) =>
        {
            Console.WriteLine($"Peer connection state change to {state}.");

            switch (state)
            {
                case RTCPeerConnectionState.connected:
                    VideoSource.OnVideoSourceEncodedSample += sendVideo;
                    Logger.LogInformation("{ConnectionId} RTC Connected", connectionId);
                    break;
                case RTCPeerConnectionState.failed:
                    pc.Close("ice disconnection");
                    break;
                case RTCPeerConnectionState.closed:
                    VideoSource.OnVideoSourceEncodedSample -= sendVideo;
                    Logger.LogInformation("{ConnectionId} RTC Disconnected", connectionId);
                    break;
            }
        };

        return pc;
    }

    public async ValueTask<string> negotiateAsync(string connectionId, RTCPeerConnection pc, string offer)
    {
        pc.setRemoteDescription(new RTCSessionDescriptionInit
        {
            type = RTCSdpType.offer,
            sdp = offer,
        });
        var answer = pc.createAnswer();
        await pc.setLocalDescription(answer);
        return answer.sdp;
    }

}

using Microsoft.AspNetCore.SignalR;
using SIPSorcery.Net;
using System.Text;
using System.Text.Json;
namespace DualDrill.Server.Services;

sealed class RTCPeerConnectionProviderService(
    RTCDemoVideoSource VideoSource,
    ILogger<RTCPeerConnectionProviderService> logger,
    IHubContext<DrillHub, IDrillHubClient> hubContext)
{
    public RTCPeerConnection CreatePeerConnection()
    {
        var pc = new RTCPeerConnection(new RTCConfiguration
        {
            iceServers = new List<RTCIceServer>()
        });
        return pc;
    }
    public async ValueTask<string> negotiateAsync(string connectionId, RTCPeerConnection pc, string offer)
    {
        var track = new MediaStreamTrack(VideoSource.VideoEncoder.SupportedFormats, MediaStreamStatusEnum.SendOnly);
        pc.addTrack(track);
        pc.ondatachannel += (channel) =>
        {
            if (channel.label == "pointermove")
            {
                channel.onmessage += (dc, protocol, data) =>
                {
                    logger.LogInformation(Encoding.UTF8.GetString(data));
                };
            }
        };
        pc.setRemoteDescription(new RTCSessionDescriptionInit
        {
            type = RTCSdpType.offer,
            sdp = offer,
        });
        pc.oniceconnectionstatechange += (state) =>
         {
             logger.LogWarning($"ice candidate state changed to {Enum.GetName(state)}");
         };
        pc.onicecandidateerror += (err, msg) =>
        {
            logger.LogError(msg + Environment.NewLine + JsonSerializer.Serialize(err));
        };
        pc.onnegotiationneeded += () =>
        {
            logger.LogWarning("Negotiation Needed");
        };
        var answer = pc.createAnswer(new RTCAnswerOptions { X_ExcludeIceCandidates = true });
        await pc.setLocalDescription(answer);

        pc.onicecandidate += (c) =>
        {
            hubContext.Clients.Client(connectionId).IceCandidate(JsonSerializer.Serialize(c));
        };

        void sendVideo(uint duration, VideoFrameBuffer buffer)
        {
            if (pc.connectionState == RTCPeerConnectionState.connected)
            {
                var s = pc.VideoStreamList.First(s => s.LocalTrack == track);
                s.SendVideo(duration, buffer.Memory.ToArray());
            }
        }

        pc.onconnectionstatechange += async (state) =>
        {
            Console.WriteLine($"Peer connection state change to {state}.");

            switch (state)
            {
                case RTCPeerConnectionState.connected:
                    VideoSource.OnVideoSourceEncodedSample += sendVideo;
                    logger.LogInformation($"{connectionId} RTC Connected");
                    break;
                case RTCPeerConnectionState.failed:
                    pc.Close("ice disconnection");
                    break;
                case RTCPeerConnectionState.closed:
                    VideoSource.OnVideoSourceEncodedSample -= sendVideo;
                    logger.LogInformation($"{connectionId} RTC Disconnected");
                    break;
            }
        };

        return answer.sdp;
    }

}

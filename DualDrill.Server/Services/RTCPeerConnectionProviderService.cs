using DualDrill.Engine.Media;
using Microsoft.AspNetCore.SignalR;
using SIPSorcery.Net;
using System.Text;
using System.Text.Json;
namespace DualDrill.Server.Services;

sealed class RTCPeerConnectionProviderService(
    HeadlessSurfaceCaptureVideoSource VideoSource,
    ILogger<RTCPeerConnectionProviderService> Logger,
    IHubContext<DrillHub, IDrillHubClient> hubContext)
{
    public RTCPeerConnection CreatePeerConnection(string connectionId)
    {
        var pc = new RTCPeerConnection(new RTCConfiguration
        {
            iceServers = new List<RTCIceServer>()
        });
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
        pc.onicecandidateerror += (err, msg) =>
        {
            Logger.LogError(msg + Environment.NewLine + JsonSerializer.Serialize(err));
        };
        pc.onnegotiationneeded += () =>
        {
            Logger.LogWarning("Negotiation Needed");
        };
        pc.onicecandidate += (c) =>
        {
            hubContext.Clients.Client(connectionId).IceCandidate(JsonSerializer.Serialize(c));
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
        pc.onconnectionstatechange += (state) =>
        {
            Console.WriteLine($"Peer connection state change to {state}.");

            switch (state)
            {
                case RTCPeerConnectionState.connected:
                    VideoSource.OnVideoSourceEncodedSample += sendVideo;
                    Logger.LogInformation($"{connectionId} RTC Connected");
                    break;
                case RTCPeerConnectionState.failed:
                    pc.Close("ice disconnection");
                    break;
                case RTCPeerConnectionState.closed:
                    VideoSource.OnVideoSourceEncodedSample -= sendVideo;
                    Logger.LogInformation($"{connectionId} RTC Disconnected");
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

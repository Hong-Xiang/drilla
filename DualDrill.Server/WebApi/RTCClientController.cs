using DualDrill.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SIPSorcery.Net;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.Abstractions;
using System.IO;
using System.Text;

namespace DualDrill.Server.WebApi;

[Route("api/[controller]")]
[ApiController]
public class RTCClientController(RTCDemoVideoSource VideoSource) : ControllerBase
{

    [HttpPost("candidate")]
    public async Task<string> AddCandidateAsync([FromBody] string candidate)
    {
        return "";
    }

    [HttpPost]
    public async Task<string> CreatePeerAsync()
    {
        string offerSdp = null;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
        {
            offerSdp = await reader.ReadToEndAsync();
        }
        var pc = new RTCPeerConnection();
        pc.addTrack(VideoSource.VideoTrack);

        VideoSource.OnVideoSourceEncodedSample += (duration, buffer) =>
        {
            pc.SendVideo(duration, buffer.Memory.ToArray());
        };

        pc.OnVideoFormatsNegotiated += (formats) => VideoSource.SetVideoSourceFormat(formats.First());

        pc.onconnectionstatechange += async (state) =>
                {
                    Console.WriteLine($"Peer connection state change to {state}.");

                    switch (state)
                    {
                        case RTCPeerConnectionState.connected:
                            await VideoSource.StartVideo();
                            Console.WriteLine("RTC Connected");
                            break;
                        case RTCPeerConnectionState.failed:
                            pc.Close("ice disconnection");
                            break;
                        case RTCPeerConnectionState.closed:
                            await VideoSource.CloseVideo();
                            Console.WriteLine("RTC Disconnected");
                            break;
                    }
                };

        pc.SetRemoteDescription(SdpType.offer, SDP.ParseSDPDescription(offerSdp));
        var answer = pc.createAnswer();
        await pc.setLocalDescription(answer);
        return answer.sdp;
    }
}

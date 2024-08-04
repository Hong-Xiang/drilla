using DualDrill.Engine.Media;
using Microsoft.AspNetCore.Mvc;
using SIPSorcery.Net;
using SIPSorcery.SIP.App;
using System.Text;

namespace DualDrill.Server.WebApi;

[Route("api/[controller]")]
[ApiController]
public class RTCClientController(HeadlessSurfaceCaptureVideoSource VideoSource) : ControllerBase
{

    [HttpPost("candidate")]
    public async Task<string> AddCandidateAsync([FromBody] string candidate)
    {
        return "";
    }



    [HttpPost]
    public async Task<string> CreatePeerAsync()
    {
        throw new NotSupportedException();
        string offerSdp = null;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
        {
            offerSdp = await reader.ReadToEndAsync();
        }
        var pc = new RTCPeerConnection();
        //pc.addTrack(VideoSource.VideoTrack);

        void sendVideo(uint duration, VideoFrameBuffer buffer)
        {
            Task.Run(() =>
            {
                if (pc.connectionState == RTCPeerConnectionState.connected)
                {
                    //pc.SendVideo(duration, buffer.Memory.ToArray());
                }
            });
        }


        pc.OnVideoFormatsNegotiated += (formats) => VideoSource.SetVideoSourceFormat(formats.First());

        pc.onconnectionstatechange += async (state) =>
                {
                    Console.WriteLine($"Peer connection state change to {state}.");

                    switch (state)
                    {
                        case RTCPeerConnectionState.connected:
                            await VideoSource.StartVideo();
                            VideoSource.OnVideoSourceEncodedSample += sendVideo;
                            Console.WriteLine("RTC Connected");
                            break;
                        case RTCPeerConnectionState.failed:
                            pc.Close("ice disconnection");
                            break;
                        case RTCPeerConnectionState.closed:
                            VideoSource.OnVideoSourceEncodedSample -= sendVideo;
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

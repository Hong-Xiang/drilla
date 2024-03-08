using Microsoft.AspNetCore.SignalR;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.Encoders;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Drilla.Server;

public interface IDrillaWebRTCClient
{
    Task<string> Negotiate(string clientId, string sdp);
    Task AddIceCandidate(string clientId, string candidate);
    Task Broadcast(string sourceId, string label, object data);
}

class WebRTCPeerHub : Hub<IDrillaWebRTCClient>
{
    public async Task Broadcast(string label, object data)
    {
        Console.WriteLine($"broadcast from ${Context.ConnectionId} @ ${label} : {data}");
        await Clients.Others.Broadcast(Context.ConnectionId, label, data);
    }

    public async Task<string> NegotiateClient(string clientId, string offerSdp)
    {
        Console.WriteLine($"Negotiate from {Context.ConnectionId} to {clientId}");
        Console.WriteLine(offerSdp);
        var answer = await Clients.Client(clientId).Negotiate(Context.ConnectionId, offerSdp).ConfigureAwait(false);
        Console.WriteLine(answer);
        return answer;
    }

    public async Task AddClientIceCandidate(string clientId, string candidate)
    {
        Console.WriteLine($"AddClientCandidate from {Context.ConnectionId} to {clientId}");
        Console.WriteLine(candidate);
        await Clients.Client(clientId).AddIceCandidate(Context.ConnectionId, candidate).ConfigureAwait(false);
    }

    public async Task<string> NegotiateServer(string offerSdp)
    {
        var testPatternSource = new VideoTestPatternSource(new VpxVideoEncoder());

        MediaStreamTrack videoTrack = new MediaStreamTrack(testPatternSource.GetVideoSourceFormats(), MediaStreamStatusEnum.SendOnly);

        var pc = new RTCPeerConnection();
        pc.addTrack(videoTrack);

        testPatternSource.OnVideoSourceEncodedSample += pc.SendVideo;
        pc.OnVideoFormatsNegotiated += (formats) => testPatternSource.SetVideoSourceFormat(formats.First());

        pc.onconnectionstatechange += async (state) =>
                {
                    Console.WriteLine($"Peer connection state change to {state}.");

                    switch (state)
                    {
                        case RTCPeerConnectionState.connected:
                            await testPatternSource.StartVideo();
                            break;
                        case RTCPeerConnectionState.failed:
                            pc.Close("ice disconnection");
                            break;
                        case RTCPeerConnectionState.closed:
                            await testPatternSource.CloseVideo();
                            testPatternSource.Dispose();
                            break;
                    }
                };

        pc.SetRemoteDescription(SdpType.offer, SDP.ParseSDPDescription(offerSdp));
        var answer = pc.createAnswer();
        await pc.setLocalDescription(answer);

        return answer.sdp;
    }
}

sealed record class TestClient(RTCPeerConnection Connection, VideoTestPatternSource VideoSource)
{
}

class RTCClientsStore
{
    public RTCDataChannel? PushChannel { get; set; }
    public RTCDataChannel? PullChannel { get; set; }

    public void TryLinkClients()
    {
        if (PushChannel is not null && PullChannel is not null)
        {
            PushChannel.onmessage += OnOffer;
            PullChannel.onmessage += OnAnswer;

            PushChannel.send(JsonSerializer.Serialize(new { kind = "link" }));
        }
    }

    private void OnOffer(RTCDataChannel channel, DataChannelPayloadProtocols protocols, byte[] data)
    {
        if (PullChannel is not null)
        {
            PullChannel.send(Encoding.UTF8.GetString(data));
        }
    }
    private void OnAnswer(RTCDataChannel channel, DataChannelPayloadProtocols protocols, byte[] data)
    {
        if (PushChannel is not null)
        {
            PushChannel.send(Encoding.UTF8.GetString(data));
        }
    }
}

public sealed record class SdpData(string Sdp)
{
}

interface IDrillPeer
{
    public Guid Id { get; }
    public Task Ready { get; }
    public Task Send(Guid source, string message);
}

sealed record class PeerMessage(Guid Target, string message)
{
}


sealed class RTCPushClientConnection
{
    public RTCPushClientConnection(RTCPeerConnection connection, Action<RTCDataChannel> onChannel)
    {
        Connection = connection;
        OnChannel = onChannel;
        Connection.ondatachannel += OnDataChannel;
    }

    private void OnDataChannel(RTCDataChannel channel)
    {
        DataChannel = channel;
        DataChannel.onmessage += OnMessage;

        OnChannel(DataChannel);
    }

    private void OnMessage(RTCDataChannel channel, DataChannelPayloadProtocols protocols, byte[] data)
    {
        Console.WriteLine(Encoding.UTF8.GetString(data));
    }

    public RTCPeerConnection Connection { get; }
    public Action<RTCDataChannel> OnChannel { get; }
    public RTCClientsStore Clients { get; }
    public RTCDataChannel DataChannel { get; private set; }
}
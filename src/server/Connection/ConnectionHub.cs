using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.Encoders;
using System.Data;

namespace Drill.Connection;

public interface IRTCHubClient
{
    Task<string?> CreateOffer(string peerConnectionId);
    Task<string?> CreateAnswer(string peerConnectionId, string? sdp);
    Task SetRemoteDescription(string peerConnectionId, string type, string? sdp);
    Task AddIceCandidate(string peerConnectionId, object candidate);
    Task WaitConnected(string peerConnectionId);

    void BroadcastFrom(string connectionId, string label, object data);
    Task PrepareNegotiate(string connectionId);
    Task RequestNegotiate(string connectionId);

    Task<string> Negotiate(string connectionId, string sdp);
}

sealed class TargetConnectionNotExistException(string connectionId) : Exception
{
    public string ConnectionId { get; } = connectionId;
}

sealed class ConnectionHub([FromServices] ConnectionStoreService ConnectionsStore) : Hub<IRTCHubClient>
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        ConnectionsStore.AddClient(Context.ConnectionId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        ConnectionsStore.RemoveClient(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }


    public void Broadcast(string label, object data)
    {
        Console.WriteLine($"broadcast from ${Context.ConnectionId} @ ${label} : {data}");
        Clients.Others.BroadcastFrom(Context.ConnectionId, label, data);
    }

    public async Task<string> Negotiate(string connectionId, string sdp)
    {
        var target = Clients.Client(connectionId) ?? throw new TargetConnectionNotExistException(connectionId);
        return await target.Negotiate(Context.ConnectionId, sdp);
    }

    public async Task Connect(string offerConnectionId, string answerConnectionId)
    {
        var offerClient = Clients.Client(offerConnectionId) ?? throw new TargetConnectionNotExistException(offerConnectionId);
        var answerClient = Clients.Client(answerConnectionId) ?? throw new TargetConnectionNotExistException(offerConnectionId);

        var offerSdp = await offerClient.CreateOffer(answerConnectionId);
        var answerSdp = await answerClient.CreateAnswer(offerConnectionId, offerSdp);
        await offerClient.SetRemoteDescription(answerConnectionId, "answer", answerSdp);

        await Task.WhenAll(offerClient.WaitConnected(answerConnectionId), answerClient.WaitConnected(offerConnectionId));

        //await answerClient.PrepareNegotiate(offerConnectionId);
        //await offerClient.RequestNegotiate(answerConnectionId);
    }



    public async Task AddIceCandidate(string connectionId, object candidate)
    {
        var target = Clients.Client(connectionId) ?? throw new TargetConnectionNotExistException(connectionId);
        Console.WriteLine($"AddClientCandidate from {Context.ConnectionId} to {connectionId}");
        Console.WriteLine(candidate);
        await target.AddIceCandidate(Context.ConnectionId, candidate).ConfigureAwait(false);
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

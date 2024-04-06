using SIPSorcery.Media;
using SIPSorcery.Net;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Drilla.Server;


//sealed record class TestClient(RTCPeerConnection Connection, VideoTestPatternSource VideoSource)
//{
//}

//class RTCClientsStore
//{
//    public RTCDataChannel? PushChannel { get; set; }
//    public RTCDataChannel? PullChannel { get; set; }

//    public void TryLinkClients()
//    {
//        if (PushChannel is not null && PullChannel is not null)
//        {
//            PushChannel.onmessage += OnOffer;
//            PullChannel.onmessage += OnAnswer;

//            PushChannel.send(JsonSerializer.Serialize(new { kind = "link" }));
//        }
//    }

//    private void OnOffer(RTCDataChannel channel, DataChannelPayloadProtocols protocols, byte[] data)
//    {
//        if (PullChannel is not null)
//        {
//            PullChannel.send(Encoding.UTF8.GetString(data));
//        }
//    }
//    private void OnAnswer(RTCDataChannel channel, DataChannelPayloadProtocols protocols, byte[] data)
//    {
//        if (PushChannel is not null)
//        {
//            PushChannel.send(Encoding.UTF8.GetString(data));
//        }
//    }
//}

//public sealed record class SdpData(string Sdp)
//{
//}

//interface IDrillPeer
//{
//    public Guid Id { get; }
//    public Task Ready { get; }
//    public Task Send(Guid source, string message);
//}

//sealed record class PeerMessage(Guid Target, string message)
//{
//}


//sealed class RTCPushClientConnection
//{
//    public RTCPushClientConnection(RTCPeerConnection connection, Action<RTCDataChannel> onChannel)
//    {
//        Connection = connection;
//        OnChannel = onChannel;
//        Connection.ondatachannel += OnDataChannel;
//    }

//    private void OnDataChannel(RTCDataChannel channel)
//    {
//        DataChannel = channel;
//        DataChannel.onmessage += OnMessage;

//        OnChannel(DataChannel);
//    }

//    private void OnMessage(RTCDataChannel channel, DataChannelPayloadProtocols protocols, byte[] data)
//    {
//        Console.WriteLine(Encoding.UTF8.GetString(data));
//    }

//    public RTCPeerConnection Connection { get; }
//    public Action<RTCDataChannel> OnChannel { get; }
//    public RTCClientsStore Clients { get; }
//    public RTCDataChannel DataChannel { get; private set; }
//}
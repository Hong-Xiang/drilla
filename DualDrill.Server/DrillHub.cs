using DualDrill.Engine;
using DualDrill.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SIPSorcery.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Channels;

namespace DualDrill.Server;

public interface IDrillHubClient
{
    Task<string> HubInvoke(string funcHandle);
    Task<string> CreateAnswer(string offer);
    Task<string> IceCandidate(string candidate);
}

sealed class DrillHub(
    RTCDemoVideoSource VideoSource,
    FrameSimulationService UpdateService,
    ILogger<DrillHub> Logger,
    IHubContext<DrillHub, IDrillHubClient> HubContext) : Hub<IDrillHubClient>
{
    static readonly string RTCConnectionKey = "RTCConnection";

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        Logger.LogInformation("{ConnectionId} Connected", Context.ConnectionId);
    }

    public async Task AddIceCandidate(string data)
    {
        var c = JsonSerializer.Deserialize<RTCIceCandidateInit>(data);
        if (c is not null)
        {
            RTCPeerConnection?.addIceCandidate(c);
        }
    }

    public async ValueTask<string> CreatePeerConnection(string offer)
    {
        var connectionId = Context.ConnectionId;
        var pc = new RTCPeerConnection(new RTCConfiguration
        {
            iceServers = new List<RTCIceServer>()
        });
        Context.Items.Add(RTCConnectionKey, pc);
        var track = new MediaStreamTrack(VideoSource.VideoEncoder.SupportedFormats, MediaStreamStatusEnum.SendOnly);
        pc.addTrack(track);
        pc.setRemoteDescription(new RTCSessionDescriptionInit
        {
            type = RTCSdpType.offer,
            sdp = offer,
        });
        pc.oniceconnectionstatechange += (state) =>
         {
             Logger.LogWarning($"ice candidate state changed to {Enum.GetName(state)}");
         };
        pc.onicecandidateerror += (err, msg) =>
        {
            Logger.LogError(msg + Environment.NewLine + JsonSerializer.Serialize(err));
        };
        pc.onnegotiationneeded += () =>
        {
            Logger.LogWarning("Negotiation Needed");
        };
        var answer = pc.createAnswer(new RTCAnswerOptions { X_ExcludeIceCandidates = true });
        await pc.setLocalDescription(answer);

        pc.onicecandidate += (c) =>
        {
            HubContext.Clients.Client(connectionId).IceCandidate(JsonSerializer.Serialize(c));
        };

        void sendVideo(uint duration, VideoFrameBuffer buffer)
        {
            Task.Run(() =>
            {
                if (pc.connectionState == RTCPeerConnectionState.connected)
                {
                    var s = pc.VideoStreamList.First(s => s.LocalTrack == track);
                    s.SendVideo(duration, buffer.Memory.ToArray());
                }
            });
        }

        pc.onconnectionstatechange += async (state) =>
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

        return answer.sdp;
    }

    public RTCPeerConnection? RTCPeerConnection
    {
        get
        {
            return Context.Items[RTCConnectionKey] as RTCPeerConnection;
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        RTCPeerConnection?.Dispose();
        await base.OnDisconnectedAsync(exception);
        if (exception is not null)
        {
            Logger.LogError(exception, "{ConnectionId} Disconnected With Expcetion", Context.ConnectionId);
        }
        else
        {
            Logger.LogInformation("{ConnectionId} Disconnected", Context.ConnectionId);
        }
    }

    public async Task<string> DoHubInvokeAsync(string funcHandle)
    {
        var action = nint.Parse(funcHandle);
        var handle = GCHandle.FromIntPtr(action);
        if (handle.Target is Func<object, ValueTask> go)
        {
            await go(this);
        }
        return "done-from-server";
    }

    public async IAsyncEnumerable<int> PingPongStream(ChannelReader<int> events)
    {
        await foreach (var e in events.ReadAllAsync().ConfigureAwait(false))
        {
            //Logger.LogInformation("Ping-Pong Stream {Data}", e);
            yield return e;
        }
    }

    public async Task MouseEvent(ChannelReader<MouseEvent> events, [FromServices] FrameInputService inputService)
    {
        inputService.AddUserEventSource(Context.ConnectionId, events);
        var tcs = new TaskCompletionSource();
        if (Context.ConnectionAborted.IsCancellationRequested)
        {
            return;
        }
        await using var r = Context.ConnectionAborted.Register(() => tcs.SetResult());
        await tcs.Task;
    }

    public async IAsyncEnumerable<RenderScene> RenderStates([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var s in UpdateService.RenderStates.Reader.ReadAllAsync(cancellationToken).WithCancellation(cancellationToken))
        {
            yield return s;
        }
    }

    public async Task<string> Echo(string data)
    {
        Console.WriteLine(data);
        return data;
    }
}

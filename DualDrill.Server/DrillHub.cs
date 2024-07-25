using DualDrill.Engine;
using DualDrill.Server.Services;
using DualDrill.Server.WebView;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.JSInterop;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using System.Numerics;
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
    IHubContext<DrillHub, IDrillHubClient> HubContext,
    WebViewService WebView) : Hub<IDrillHubClient>
{
    static readonly string RTCConnectionKey = "RTCConnection";

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public async Task IceCandidateFromClient(string data)
    {
        var c = JsonSerializer.Deserialize<RTCIceCandidateInit>(data);
        if (c is not null)
        {
            RTCPeerConnection?.addIceCandidate(c);
        }
    }

    public async ValueTask<string> CreatePeerConnection(string offer)
    {
        if (RTCPeerConnection is not null)
        {

        }
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
        Logger.LogWarning($"{Context.ConnectionId} SignalR Disconnected");
        RTCPeerConnection?.Dispose();
        await base.OnDisconnectedAsync(exception);
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
    public async IAsyncEnumerable<SharedBufferMessage> SharedBufferServerRenderingReadable(CancellationToken cancellation)
    {
        await foreach (var b in WebView.GetAllReadableSlotsAsync(cancellation).ConfigureAwait(false))
        {
            yield return b.Message;
        }
    }


    public async Task SharedBufferServerRenderingWriteable(ChannelReader<SharedBufferMessage> writeable)
    {
        await foreach (var m in writeable.ReadAllAsync(Context.ConnectionAborted).ConfigureAwait(false))
        {
            await WebView.SetReadyToWriteAsync(m, Context.ConnectionAborted);
        }
    }

    public async Task MouseEvent(ChannelReader<MouseEvent> events, [FromServices] FrameInputService inputService)
    {
        //var writer = UpdateService.MouseEvents.Writer;
        //await foreach (var e in events.ReadAllAsync().ConfigureAwait(false))
        //{
        //    await writer.WriteAsync(e).ConfigureAwait(false);
        //}

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

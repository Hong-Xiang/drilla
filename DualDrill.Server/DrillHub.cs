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
    IHubContext<DrillHub, IDrillHubClient> HubContext,
    RTCPeerConnectionProviderService RTCPeerConnectionProviderService) : Hub<IDrillHubClient>
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

    public async ValueTask<string> Negotiate(string offer)
    {
        if (RTCPeerConnection is null)
        {
            var pc = RTCPeerConnectionProviderService.CreatePeerConnection();
            RTCPeerConnection = pc;
        }
        return await RTCPeerConnectionProviderService.negotiateAsync(Context.ConnectionId, RTCPeerConnection, offer);
    }

    public RTCPeerConnection? RTCPeerConnection
    {
        get
        {
            if (Context.Items.TryGetValue(RTCConnectionKey, out var pc))
            {
                return pc as RTCPeerConnection;
            }
            else
            {
                return null;
            }
        }
        set
        {
            if (Context.Items.TryGetValue(RTCConnectionKey, out _))
            {
                Context.Items[RTCConnectionKey] = value;
            }
            else
            {
                Context.Items.Add(RTCConnectionKey, value);
            }
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

    public async IAsyncEnumerable<int> PingPongStream(ChannelReader<int> events, [EnumeratorCancellation] CancellationToken cancellation)
    {
        await foreach (var e in events.ReadAllAsync(cancellation))
        {
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
        await foreach (var s in UpdateService.RenderStates.Reader.ReadAllAsync(cancellationToken))
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

﻿using DualDrill.Common;
using DualDrill.Engine.Connection;
using DualDrill.Engine.Event;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace DualDrill.Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SignalConnectionController(
    ILogger<SignalConnectionController> Logger,
    ISignalConnectionProviderService SignalConnectionService,
    IPeerConnectionProviderService PeerConnectionProvider
) : ControllerBase
{
    [HttpGet]
    [Route("server")]
    public IActionResult GetServerId()
    {
        return Ok(new
        {
            id = ClientsManager.ServerId
        });
    }

    [HttpPost]
    [Route("AddIceCandidate/{sourceId}/{targetId}")]
    public async Task AddIceCandidate(Guid sourceId, Guid targetId, [FromBody] string candidate, CancellationToken cancellation)
    {
        Logger.LogInformation("AddIceCandidate from {sourceId} to {targetId}, content {Content}", sourceId, targetId, candidate);
        await SignalConnectionService.AddIceCandidateAsync(new(sourceId, targetId, new(candidate)), cancellation);
    }

    [HttpPost]
    [Route("Offer/{sourceId}/{targetId}")]
    public async Task OfferAsync(Guid sourceId, Guid targetId, [FromBody] string sdp, CancellationToken cancellation)
    {
        Logger.LogInformation("Offer from {sourceId} to {targetId}", sourceId, targetId);
        await SignalConnectionService.OfferAsync(new(sourceId, targetId, new(sdp)), cancellation);
    }

    [HttpPost]
    [Route("Answer/{sourceId}/{targetId}")]
    public async Task AnswerAsync(Guid sourceId, Guid targetId, [FromBody] string sdp, CancellationToken cancellation)
    {
        Logger.LogInformation("Answer from {sourceId} to {targetId}", sourceId, targetId);
        await SignalConnectionService.AnswerAsync(new(sourceId, targetId, new(sdp)), cancellation);
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("/ws/signal-connection/{clientId}")]
    public async Task GetAsync(Guid clientId, CancellationToken cancellation)
    {
        try
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                // TODO: find proper way (e.g. await Ping/Pong) to ensure the event listener in client is added
                await Task.Delay(2000);
                await PeerConnectionProvider.CreatePeerConnectionAsync(clientId, cancellation);
                await SendAllMessageAsync(webSocket, clientId, cancellation);
            }
            else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }
        catch (OperationCanceledException e)
        {
            Logger.LogError(e, "Signal Server Connection Cancelled");
        }
    }

    async ValueTask SendMessageAsync(WebSocket webSocket, byte[] buffer, string message, CancellationToken cancellation)
    {
        var messageLength = Encoding.UTF8.GetByteCount(message);
        Debug.Assert(messageLength <= buffer.Length);
        Encoding.UTF8.GetBytes(message, buffer);
        Logger.LogInformation("Sending message to payload: {Message}", message);
        await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, messageLength),
            WebSocketMessageType.Text,
            true,
            cancellation);
    }

    async ValueTask SendAllMessageAsync(WebSocket webSocket, Guid clientId, CancellationToken cancellation)
    {
        var pipe = new Pipe();

        const int BufferSize = 1024 * 64;

        Channel<string> channel = Channel.CreateUnbounded<string>();
        using var addSub = SignalConnectionService.SubscribeAddIceCandidateAwait(clientId, async (e, c) =>
                                 {
                                     await channel.Writer.WriteAsync(JsonSerializer.Serialize(TaggedEvent.Create(e), CustomJsonOption.Web), c);
                                 });
        using var offerSub = SignalConnectionService.SubscribeOfferAwait(clientId, async (e, c) =>
                                 {
                                     await channel.Writer.WriteAsync(JsonSerializer.Serialize(TaggedEvent.Create(e), CustomJsonOption.Web), c);
                                 });
        using var answerSub = SignalConnectionService.SubscribeAnswerAwait(clientId, async (e, c) =>
                                 {
                                     await channel.Writer.WriteAsync(JsonSerializer.Serialize(TaggedEvent.Create(e), CustomJsonOption.Web), c);
                                 });


        await foreach (var c in channel.Reader.ReadAllAsync(cancellation))
        {
            async Task WriteAllAsync()
            {
                var writer = pipe.Writer;
                await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(c), cancellation);
            }
            async Task SendAllAsync()
            {
                ReadOnlyMemory<byte>? last = null;
                var reader = pipe.Reader;
                var result = await reader.ReadAsync(cancellation);
                try
                {
                    foreach (var r in result.Buffer)
                    {
                        if (last.HasValue)
                        {
                            await webSocket.SendAsync(last.Value, WebSocketMessageType.Text, false, cancellation);
                        }
                        last = r;
                    }
                    if (last.HasValue)
                    {
                        await webSocket.SendAsync(last.Value, WebSocketMessageType.Text, true, cancellation);
                    }
                }
                finally
                {
                    reader.AdvanceTo(result.Buffer.End);
                }
            }
            await Task.WhenAll(WriteAllAsync(), SendAllAsync());
        }
    }
}

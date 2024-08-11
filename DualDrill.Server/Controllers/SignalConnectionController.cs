using Devlooped.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Silk.NET.OpenAL;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Text;

namespace DualDrill.Server.Controllers;

[ApiController]
public class SignalConnectionController : ControllerBase
{
    [HttpGet]
    [Route("/ws/signal-connection/{clientId}")]
    public async Task GetAsync(Guid clientId, CancellationToken cancellation)
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            Console.WriteLine($"Get Websocket Connection from client id {clientId}");
            await EchoAsync(webSocket, cancellation);
        }
        else
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }

    async ValueTask EchoAsync(WebSocket webSocket, CancellationToken cancellation)
    {
        const int MinimumBufferSize = 1024 * 64;

        var buffer = new byte[MinimumBufferSize];
        var receiveResult = await webSocket.ReceiveAsync(
            new ArraySegment<byte>(buffer), cancellation);

        while (!receiveResult.CloseStatus.HasValue)
        {
            Console.WriteLine(Encoding.UTF8.GetString(buffer.AsSpan()[..receiveResult.Count]));
            await webSocket.SendAsync(
                new ArraySegment<byte>(buffer, 0, receiveResult.Count),
                receiveResult.MessageType,
                receiveResult.EndOfMessage,
                cancellation);


            receiveResult = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer), cancellation);
        }

        await webSocket.CloseAsync(
            receiveResult.CloseStatus.Value,
            receiveResult.CloseStatusDescription,
            cancellation);
    }
}

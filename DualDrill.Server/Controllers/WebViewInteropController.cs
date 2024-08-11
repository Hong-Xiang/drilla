using DualDrill.Engine;
using DualDrill.WebView.Interop;
using MessagePipe;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DualDrill.Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public sealed class WebViewInteropController(
    IWebViewService WebViewService,
    IPublisher<SharedBufferReceivedEvent> SharedBufferReceivedPublisher,
    IPublisher<Guid, CapturedStream> CapturedStream
) : ControllerBase
{
    [HttpGet]
    [Route("test")]
    public async Task<IActionResult> TestMessage()
    {
        await WebViewService.SendMessage("test message from server", CancellationToken.None);
        return Ok("1");
    }

    [HttpPost]
    [Route("sharedbuffer/{id}")]
    public IActionResult SharedBufferReceived(Guid id)
    {
        SharedBufferReceivedPublisher.Publish(new(id));
        return Ok();
    }

    [HttpPost]
    [Route("capturedStream/{surfaceId}/{streamId}")]
    public IActionResult CapturedStreamCreated(Guid surfaceId, string streamId)
    {
        CapturedStream.Publish(surfaceId, new(surfaceId, streamId));
        return Ok();
    }
}

using DualDrill.Engine;
using DualDrill.Engine.Headless;
using DualDrill.WebView.Interop;
using MessagePipe;
using Microsoft.AspNetCore.Mvc;

namespace DualDrill.Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public sealed class WebViewInteropController(
    IWebViewService WebViewService,
    IWebViewInteropService WebViewInteropService,
    IPublisher<SharedBufferReceivedEvent> SharedBufferReceivedPublisher,
    IPublisher<Guid, CapturedStream> CapturedStream,
    HeadlessSurface Surface
) : ControllerBase
{
    [HttpGet]
    [Route("MainSurface")]
    public async Task<IActionResult> GetMainSurface()
    {
        return Ok(Surface.Entity);
    }


    [HttpGet]
    [Route("SurfaceSharedBuffer")]
    public async Task<IActionResult> GetSharedBufferAsync([FromQuery] Guid? surfaceId, CancellationToken cancellation)
    {
        if (surfaceId.HasValue && surfaceId.Value != Surface.Id)
        {
            throw new NotImplementedException();
        }
        _ = await WebViewInteropService.CreateSurfaceSharedBufferAsync(
               Surface,
               cancellation);
        return Ok(Surface.Entity);
    }

    [HttpPost]
    [Route("SurfaceSharedBuffer/{id}")]
    public IActionResult SharedBufferReceived(Guid id)
    {
        SharedBufferReceivedPublisher.Publish(new(id));
        return Ok();
    }

    [HttpPost]
    [Route("SurfaceSharedBuffer/{id}/OnFrameSubscription")]
    public async Task<IActionResult> SubscribeSurfaceOnFrameEventAsync(Guid id, CancellationToken cancellation)
    {
        return Ok();
    }
}

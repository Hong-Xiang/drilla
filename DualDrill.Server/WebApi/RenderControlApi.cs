using DualDrill.Engine;
using DualDrill.Graphics;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Runtime.InteropServices;

namespace DualDrill.Server.WebApi;

public static class RenderControlApi
{
    public static Task<IActionResult> StartRender()
    {
        throw new NotImplementedException();
    }

    public static Task<IActionResult> Stop()
    {
        throw new NotImplementedException();
    }
    public static async Task<IResult> DoRender([FromServices] SimpleTriangleRendererLegacy renderService)
    {
        renderService.Render();
        return Results.Ok();
    }
    public static async Task<IResult> RenderToImage(
        [FromServices] DualDrill.Engine.Renderer.SimpleColorRenderer renderer,
        [FromServices] GPUDevice device,
        [FromQuery(Name = "time")] int time,
        [FromQuery(Name = "width")] int width = 512,
        [FromQuery(Name = "height")] int height = 512
    )
    {
        using var target = new DualDrill.Engine.Headless.HeadlessRenderTarget(device, width, height, GPUTextureFormat.BGRA8UnormSrgb);
        using var queue = device.GetQueue();
        await renderer.RenderAsync(time, queue, target.Texture).ConfigureAwait(false);
        var data = await target.ReadResultAsync(default).ConfigureAwait(false);
        var image = Image.LoadPixelData<Bgra32>(data.Span, width, height);
        var stream = new MemoryStream();
        await image.SaveAsPngAsync(stream).ConfigureAwait(false);
        stream.Position = 0;
        return Results.File(stream, "image/png");
    }

    public static async Task<IResult> GetImage([FromQuery(Name = "handle")] nint HandlePtr)
    {
        var handle = GCHandle.FromIntPtr(HandlePtr);
        var image = (SixLabors.ImageSharp.Image<Bgra32>)handle.Target;
        handle.Free();
        var ms = new MemoryStream();
        await image.SaveAsPngAsync(ms).ConfigureAwait(false);
        ms.Position = 0;
        return Results.File(ms, "image/png");
    }

    public static void MapRenderControls(this WebApplication app)
    {
        app.MapPost("/api/render", StartRender);
        app.MapDelete("/api/render", StartRender);
        app.MapGet("/api/doRender", DoRender);
        app.MapGet("/api/render-headless", RenderToImage);
    }
}


using DualDrill.Engine;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Diagnostics;
using System.IO;
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
    public static async Task<IResult> VulkanRender([FromServices] VulkanHeadlessService renderService)
    {
        var data = renderService.Render();
        var image = SixLabors.ImageSharp.Image.LoadPixelData<SixLabors.ImageSharp.PixelFormats.Bgra32>(data,
            VulkanHeadlessService.WIDTH,
            VulkanHeadlessService.HEIGHT);
        var stream = new MemoryStream();
        await image.SaveAsJpegAsync(stream);
        stream.Position = 0;
        return Results.File(stream, "image/jpeg");
    }

    public static async Task<IResult> WGPUHeadless(
        [FromServices] TriangleRenderer wGPUHeadless,
        [FromServices] HeadlessRenderTargetPool targetsPool,
        [FromQuery(Name = "time")] int time
    )
    {
        var sw = Stopwatch.StartNew();
        var target = targetsPool.Rent();
        await wGPUHeadless.Render((double)time / 1000, target.Texture).ConfigureAwait(false);
        var renderTime = sw.Elapsed.TotalMilliseconds;
        var imageData = await target.ReadResultAsync(default).ConfigureAwait(false);
        var readBackTime = sw.Elapsed.TotalMilliseconds - renderTime;
        var result = targetsPool.RentResultBuffer();
        //var result = imageData.ToArray();
        imageData.CopyTo(result);
        targetsPool.Return(target);
        var dataPrepareTime = sw.Elapsed.TotalMilliseconds - renderTime - readBackTime;
        //var ms = new MemoryStream();
        //await ms.WriteAsync(image);
        //ms.Position = 0;
        //wGPUHeadless.ResultBufferPool.Return(image);
        //await image.SaveAsPngAsync(ms).ConfigureAwait(false);
        //ms.Position = 0;
        //return Results.File(ms, "image/png");
        Console.WriteLine($"RenderTime {renderTime:0.00}ms, readbackTime {readBackTime:0.00}ms dataPrepareTime {dataPrepareTime:0.00}ms, total: {sw.Elapsed.TotalMilliseconds:0.00}");
        return Results.File(result, "application/x-frame-content");
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
        app.MapGet("/api/vulkan/render", VulkanRender);
        app.MapGet("/api/wgpu/render", WGPUHeadless);
        app.MapGet("/api/render-result", WGPUHeadless);
    }
}


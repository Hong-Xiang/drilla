﻿using DualDrill.Engine;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.JSInterop;
using SixLabors.ImageSharp;

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
    public static async Task<IResult> DoRender([FromServices] WebGPUHeadlessService renderService)
    {
        await renderService.Render();
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

    public static void MapRenderControls(this WebApplication app)
    {
        app.MapPost("/api/render", StartRender);
        app.MapDelete("/api/render", StartRender);
        app.MapGet("/api/doRender", DoRender);
        app.MapGet("/api/vulkan/render", VulkanRender);
    }
}


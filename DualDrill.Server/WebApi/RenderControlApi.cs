using DualDrill.Engine;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

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
        renderService.Render();
        return Results.Ok();
    }


    public static void MapRenderControls(this WebApplication app)
    {
        app.MapPost("/api/render", StartRender);
        app.MapDelete("/api/render", StartRender);
        app.MapGet("/api/doRender", DoRender);
    }
}


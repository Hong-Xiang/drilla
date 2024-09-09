using DualDrill.Engine.Scene;
using DualDrill.Engine.Services;
using DualDrill.Graphics;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Numerics;
using Image = SixLabors.ImageSharp.Image;

namespace DualDrill.Server.Controllers;

[Route("render")]
public class RenderController(
    GPUDevice Device,
    IGPUDevice Device2,
    IFrameRenderService RenderService
) : Controller
{
    [HttpGet("repl")]
    public IActionResult REPL()
    {
        return View();
    }

    [HttpGet("cube")]
    public async Task<IResult> RenderToImage(
        [FromQuery(Name = "time")] int time = 0,
        [FromQuery(Name = "width")] int width = 640,
        [FromQuery(Name = "height")] int height = 480,
        [FromQuery(Name = "cameraX")] float cameraX = 1.0f,
        [FromQuery(Name = "cameraY")] float cameraY = 1.0f,
        [FromQuery(Name = "cameraZ")] float cameraZ = 1.0f,
        [FromQuery(Name = "lookAtX")] float lookAtX = 0.0f,
        [FromQuery(Name = "lookAtY")] float lookAtY = 0.0f,
        [FromQuery(Name = "lookAtZ")] float lookAtZ = 0.0f,
        [FromQuery(Name = "upX")] float upX = 0.0f,
        [FromQuery(Name = "upY")] float upY = 1.0f,
        [FromQuery(Name = "upZ")] float upZ = 0.0f,
        [FromQuery(Name = "scale")] float scale = 1000.0f,
        [FromQuery(Name = "cubePositionX")] float cubePositionX = 0.0f,
        [FromQuery(Name = "cubePositionY")] float cubePositionY = 0.0f,
        [FromQuery(Name = "cubePositionZ")] float cubePositionZ = 0.0f,
        [FromQuery(Name = "cubeRotationX")] float cubeRotationX = 0.0f,
        [FromQuery(Name = "cubeRotationY")] float cubeRotationY = 0.0f,
        [FromQuery(Name = "cubeRotationZ")] float cubeRotationZ = 0.0f,

        CancellationToken cancellation = default)
    {
        using var target = new DualDrill.Engine.Headless.HeadlessRenderTarget(Device, width, height, GPUTextureFormat.BGRA8UnormSrgb);
        var scene = RenderScene.TestScene(width, height);
        var pos = new Vector3(cameraX, cameraY, cameraZ);
        scene = scene with
        {
            Cube = scene.Cube with
            {
                Position = new(cubePositionX, cubePositionY, cubePositionZ),
                Rotation = new(cubeRotationX, cubeRotationY, cubeRotationZ)
            },
            Camera = scene.Camera with
            {
                NearPlaneWidth = width / scale,
                NearPlaneHeight = height / scale,
                Position = pos,
                Forward = new Vector3(lookAtX, lookAtY, lookAtZ) - pos,
                Up = new Vector3(upX, upY, upZ)
            }
        };

        await RenderService.RenderAsync(time, scene, target.Texture, cancellation);
        var data = await target.ReadResultAsync(cancellation);
        var image = Image.LoadPixelData<Bgra32>(data.Span, width, height);
        var stream = new MemoryStream();
        await image.SaveAsPngAsync(stream, cancellation);
        stream.Position = 0;
        return Results.File(stream, "image/png");
    }


}

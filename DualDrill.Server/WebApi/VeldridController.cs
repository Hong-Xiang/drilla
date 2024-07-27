using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using Veldrid;
using Veldrid.SPIRV;
using Veldrid.Utilities;

namespace DualDrill.Server.WebApi;




[Route("api/[controller]")]
[ApiController]
public class VeldridController : ControllerBase
{
    private readonly GraphicsDevice Device;
    private readonly ResourceFactory Factory;
    private readonly Texture OffscreenColor;
    private readonly uint Width = 800;
    private readonly uint Height = 600;
    public VeldridController() : base()
    {
        Device = GraphicsDevice.CreateVulkan(new GraphicsDeviceOptions
        {
            Debug = true,
            SwapchainDepthFormat = PixelFormat.R16_UNorm,
            SyncToVerticalBlank = true,
            ResourceBindingModel = ResourceBindingModel.Improved,
            PreferDepthRangeZeroToOne = true,
            PreferStandardClipSpaceYDirection = true,
        });
        Factory = new DisposeCollectorResourceFactory(Device.ResourceFactory);
        OffscreenColor = Factory.CreateTexture(
            TextureDescription.Texture2D(
                Width, Height, 1, 1, PixelFormat.B8_G8_R8_A8_UNorm, TextureUsage.RenderTarget | TextureUsage.Staging
            )
        );
    }
    [HttpGet]
    public async Task<IResult> HeadlessRenderAsync(
        CancellationToken cancellation)
    {
        ReadOnlyMemory<Bgra32> data = null;
        var image = SixLabors.ImageSharp.Image.LoadPixelData<SixLabors.ImageSharp.PixelFormats.Bgra32>(data.Span, (int)Width, (int)Height);
        var stream = new MemoryStream();
        await image.SaveAsPngAsync(stream).ConfigureAwait(false);
        stream.Position = 0;
        return Results.File(stream, "image/png");

    }
}


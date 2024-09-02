using DualDrill.Engine.Scene;
using DualDrill.Engine.Services;
using DualDrill.Graphics;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Numerics;
using Image = SixLabors.ImageSharp.Image;

namespace DualDrill.Server.WebApi;

public static class RenderControlApi
{
    public static void MapRenderControls(this WebApplication app)
    {
        //app.MapGet("/render", RenderToImage);
    }
}


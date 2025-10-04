using DualDrill.Engine.Headless;
using DualDrill.Server.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace DualDrill.Server.Controllers;

[Route("")]
[Route("/home")]
public class HomeController(
    ILogger<HomeController> Logger,
    HeadlessSurface Surface
) : Controller
{
    [HttpGet("")]
    [HttpGet("index")]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("privacy")]
    public IActionResult Privacy()
    {
        return View();
    }

    [HttpGet("desktop")]
    public IActionResult Desktop()
    {
        ViewData["Width"] = Surface.Width;
        ViewData["Height"] = Surface.Height;
        Logger.LogInformation("Desktop Client Request Received");
        return View();
    }

    [HttpGet("webview2")]
    public IActionResult WebView2()
    {
        ViewData["Width"] = Surface.Width;
        ViewData["Height"] = Surface.Height;
        return View();
    }

    [HttpGet("volume")]
    public IActionResult VolumeRendering()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [HttpGet("error")]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

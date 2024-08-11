using DualDrill.Engine.Headless;
using Microsoft.AspNetCore.Mvc;
using DualDrill.Server.Models;
using System.Diagnostics;

namespace DualDrill.Server.Controllers;

public class HomeController(
    ILogger<HomeController> Logger,
    HeadlessSurface Surface
) : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult Desktop()
    {
        ViewData["Width"] = Surface.Width;
        ViewData["Height"] = Surface.Height;
        Logger.LogInformation("Desktop Client Request Received");
        return View();
    }

    public IActionResult WebView2()
    {
        ViewData["Width"] = Surface.Width;
        ViewData["Height"] = Surface.Height;
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

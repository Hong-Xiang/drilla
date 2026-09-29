using DualDrill.Server.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace DualDrill.Server.Controllers;

[Route("")]
[Route("/home")]
public class HomeController : Controller
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

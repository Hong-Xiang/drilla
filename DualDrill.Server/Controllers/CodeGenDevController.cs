using DualDrill.ApiGen.DMath;
using Microsoft.AspNetCore.Mvc;

namespace DualDrill.Server.Controllers;

[Route("codegen")]
public class CodeGenDevController : Controller
{
    [HttpGet("develop")]
    public IActionResult Develop()
    {
        var codeGen = new DMathCodeGen();
        ViewData["Code"] = codeGen.Generate();
        return View();
    }
}

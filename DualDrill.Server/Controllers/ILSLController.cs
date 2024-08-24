using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace DualDrill.Server.Controllers;

[Route("/ilsl")]
public class ILSLController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["ExpectedCode"] = DualDrill.Engine.Shader.ExpectedResult.Code;
        ViewData["GeneratedCode"] = ILSL.ILSLCompiler.Compile<Engine.Shader.ShaderModule>();
        ViewData["AstJson"] = JsonSerializer.Serialize(ILSL.ILSLCompiler.ASTToJson<Engine.Shader.ShaderModule>(),
            new JsonSerializerOptions()
            {
                WriteIndented = true
            });
        return View();
    }
}

using DualDrill.Engine.Shader;
using Microsoft.AspNetCore.Mvc;

namespace DualDrill.Server.Controllers;

[Route("[controller]")]
public class ILSLController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("wgsl/expected")]
    public IActionResult ExpectedCode()
    {
        return Ok(IDevelopILSLExpectedCode.GetCode<Engine.Shader.MinimumTriangle>());
    }


    [HttpGet("wgsl")]
    public IActionResult GeneratedCode()
    {
        var code = ILSL.ILSLCompiler.Compile<Engine.Shader.MinimumTriangle>();
        return Ok(code);
    }

    [HttpGet("ast")]
    public IActionResult GeneratedAst()
    {
        return Ok(ILSL.ILSLCompiler.ASTToJson<Engine.Shader.MinimumTriangle>());
    }
}

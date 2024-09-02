using DualDrill.Engine.Shader;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using System.Text.Json;

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

    [HttpGet("methodIL")]
    public IActionResult GetIL()
    {
        var mb = this.GetType().GetMethod(nameof(TestMethod)).GetMethodBody();
        return File(mb.GetILAsByteArray(), "application/octet-stream");
    }

    [HttpGet("methodCode")]
    public IActionResult DecompileMethod()
    {
        var m = this.GetType().GetMethod(nameof(TestMethod));
        return Ok(ILSL.ILSLCompiler.CompileMethod(m));
    }

    public int TestMethod(int a, int b)
    {
        return a + b;
    }

    [HttpGet("ast")]
    public IActionResult GeneratedAst()
    {
        return Ok(ILSL.ILSLCompiler.ASTToJson<Engine.Shader.MinimumTriangle>());
    }
}

using DualDrill.Engine.Shader;
using DualDrill.ILSL;
using DualDrill.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace DualDrill.Server.Controllers;

[Route("[controller]")]
public class ILSLController(ILSLDevelopShaderModuleService ShaderModules) : Controller
{
    IShaderModule? GetShaderModule(string name)
    {
        return ShaderModules.ShaderModules[name];
    }

    IILSLDevelopShaderModule? GetDevelopmentShaderModule(string name)
    {
        return ShaderModules.ShaderModules[name];
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("wgsl/{name}/expected")]
    public IActionResult ExpectedCode(string name)
    {
        return Ok(GetDevelopmentShaderModule(name).ILSLWGSLExpectedCode);
    }


    [HttpGet("wgsl/{name}")]
    public IActionResult GeneratedCode(string name)
    {
        var shaderModule = GetShaderModule(name);
        if (shaderModule is null)
        {
            return NotFound();
        }
        var code = ILSL.ILSLCompiler.Compile(shaderModule);
        return Ok(code);
    }

    [HttpGet("ast/{name}")]
    public IActionResult GeneratedAst(string name)
    {
        var shaderModule = GetShaderModule(name);
        if (shaderModule is null)
        {
            return NotFound();
        }
        return Ok(ILSL.ILSLCompiler.ASTToJson(shaderModule));
    }
}

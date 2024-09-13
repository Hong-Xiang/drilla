using DualDrill.Engine.Shader;
using DualDrill.ILSL;
using DualDrill.ILSL.IR.Declaration;
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
    public async Task<IActionResult> GeneratedCode(string name)
    {
        var shaderModule = GetShaderModule(name);
        if (shaderModule is null)
        {
            return NotFound();
        }
        var code = ILSL.ILSLCompiler.Compile(shaderModule);

        //return await GenerateCodeUsingIRAsync(); 
        return Ok(code);
    }

    [HttpGet("wgsl/{name}/ir")]
    public async Task<IActionResult> GeneratedCodeUsingIR(string name)
    {
        var shaderModule = GetShaderModule(name);
        if (shaderModule is null)
        {
            return NotFound();
        }
        var code = ILSL.ILSLCompiler.CompileIR(shaderModule);

        //return await GenerateCodeUsingIRAsync(); 
        return Ok(code);
    }

    [HttpGet("wgsl/ir")]
    public async Task<IActionResult> GenerateCodeUsingIRAsync()
    {
        var module = MinimumTriangleModule.CreateModule();
        var tw = new StringWriter();
        var wgslVisitor = new ModuleToCodeVisitor(tw, new WGSLLanguage());
        foreach (var d in module.Declarations)
        {
            await d.AcceptVisitor(wgslVisitor);
        }
        return Ok(tw.ToString());
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

using DualDrill.Engine.Shader;
using DualDrill.ILSL;
using DualDrill.ILSL.Frontend;
using DualDrill.ILSL.IR.Declaration;
using DualDrill.Server.Services;
using ICSharpCode.Decompiler.Metadata;
using Microsoft.AspNetCore.Mvc;
using System.Numerics;

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
        return Ok(await ILSLCompiler.Compile(shaderModule));
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

    [HttpGet("ilreader")]
    public IActionResult TryRead()
    {
        var parser = new RuntimeILParser();
        var method = typeof(ILSLController).GetMethod("TestMethod2");
        //var ops = ILSLCompiler.ILReader(method);
        //return Ok(ops.Select(op => (op, op.GetDisplayName())));
        var code = ILSLCompiler.ILReaderFromRuntimeAssembly(method);
        return Ok(code);
    }

    public int TestMethod(int a, int b)
    {
        return a * b;
    }

    public Vector4 TestMethod2()
    {
        return new Vector4(0.1f, 0.1f, 0.4f, 0.5f);
    }
}

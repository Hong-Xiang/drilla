﻿using DualDrill.Engine.Shader;
using DualDrill.ILSL;
using DualDrill.ILSL.Frontend;
using DualDrill.ILSL.IR.Declaration;
using DualDrill.Server.Services;
using ICSharpCode.Decompiler.Metadata;
using Lokad.ILPack.IL;
using Microsoft.AspNetCore.Mvc;
using System.Numerics;
using System.Reflection;

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
    public async Task<IActionResult> CompileModule(string name)
    {
        var shaderModule = GetShaderModule(name);
        if (shaderModule is null)
        {
            return NotFound();
        }
        return Ok(await ILSLCompiler.Compile(shaderModule));
    }

    [HttpGet("parse/{name}")]
    public async Task<IActionResult> ParseModule(string name)
    {
        var shaderModule = GetShaderModule(name);
        if (shaderModule is null)
        {
            return NotFound();
        }
        var ir = ILSL.ILSLCompiler.Parse(shaderModule);
        return Ok(ir);
    }


    async Task<IActionResult> MethodTargetAction(string moduleName, string methodName,
            Func<ILSpyFrontend, MethodBase, Task<IActionResult>> next)
    {
        var shaderModule = GetShaderModule(moduleName);
        if (shaderModule is null)
        {
            return NotFound($"Module {moduleName} not found");
        }
        var shaderModuleType = shaderModule.GetType();
        var method = shaderModuleType.GetMethod(methodName,
            System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Static
            | System.Reflection.BindingFlags.Instance);
        if (method is null)
        {
            return NotFound($"Method {methodName} not found");
        }
        var parser = new ILSpyFrontend(new ILSpyOption
        {
            HotReloadAssemblies = [shaderModuleType.Assembly]
        });
        return await next(parser, method);
    }

    [HttpGet("decompile/{moduleName}/method/{methodName}")]
    public async Task<IActionResult> DecompileMethodAsync(string moduleName, string methodName)
    {
        return await MethodTargetAction(moduleName, methodName, async (parser, method) =>
        {
            var ast = parser.Decompile(method);
            return Ok(ast.ToString());
        });
    }

    [HttpGet("parse/{moduleName}/method/{methodName}")]
    public async Task<IActionResult> ParseMethodAsync(string moduleName, string methodName)
    {
        return await MethodTargetAction(moduleName, methodName, async (parser, method) =>
        {
            var ir = parser.ParseMethod(method);
            return Ok(ir);
        });
    }

    [HttpGet("compile/{moduleName}/method/{methodName}")]
    public async Task<IActionResult> CompileMethodAsync(string moduleName, string methodName)
    {
        return await MethodTargetAction(moduleName, methodName, async (parser, method) =>
        {
            var ir = parser.ParseMethod(method);
            var module = new ILSL.IR.Module([ir]);
            var code = await module.EmitCode();
            return Ok(code);
        });
    }

    [HttpGet("ilreader")]
    public IActionResult ILReaderTest()
    {

        var method = typeof(ILSLController).GetMethod(nameof(TestMethod2), BindingFlags.NonPublic | BindingFlags.Instance);
        if (method is null)
        {
            return NotFound("method not found");
        }
        var ops = ILSLCompiler.ILReader(method);
        //return Ok(ops);
        var insts = method.GetInstructions();
        return Ok(insts.Select(s =>
        new {
            offset = s.Offset,
            opCode = s.OpCode,
            operand = s.Operand?.ToString()
        }));
    }

    private int TestMethod(int a, int b)
    {
        return a + b;
    }

    private Vector4 TestMethod2()
    {
        return new Vector4(0.0f, 0.1f, 0.2f, 0.3f);
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

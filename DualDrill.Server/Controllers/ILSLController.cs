﻿using DualDrill.CLSL;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.Engine.Shader;
using DualDrill.Server.Services;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using System.Text.Json;

namespace DualDrill.Server.Controllers;

[Route("[controller]")]
public class ILSLController(ILSLDevelopShaderModuleService ShaderModules, ICLSLCompiler Compiler) : Controller
{
    ISharpShader? GetShader(string name)
    {
        if (ShaderModules.ShaderModules.TryGetValue(name, out var shader))
        {
            return shader;
        }
        else
        {
            return null;
        }
    }


    struct DevelopShader : ISharpShader
    {
        [Vertex]
        public static float Test(float a, float b, float c)
        {
            return c <= 0 ? a : b;
        }
    }

    [HttpGet("compile/develop")]
    public async Task<IActionResult> CompileDevelop()
    {
        var shader = new DevelopShader();
        //ShaderModuleDeclaration ir = await Compiler.ParseAsync(shader);
        //var pass0 = ShaderModuleCompilationPass.Create(new DotNetInstructionPass());
        //ir = pass0.Compile(ir);
        //var pass1 = ShaderModuleCompilationPass.Create(new ControlFlowGraphDotNetInstructionPass());
        //ir = pass1.Compile(ir);
        //var context = ShaderModuleCompilationContext.Create();
        //var methodContext = context.GetMethodContext(MethodHelper.GetMethod<float, float, float, float>(DevelopShader.Test));
        //var pass2 = ShaderModuleCompilationPass.Create(new ControlFlowGraphPass(methodContext));
        var code = await Compiler.EmitWGSL(shader);
        return Ok(code);
    }

    [HttpGet("compile/{name}/ir")]
    public IActionResult ParseDevelopModule(string name)
    {
        var shader = GetShader(name);
        if (shader is null)
        {
            return NotFound();
        }
        var ir = shader.Parse();
        //return Ok(ir.Dump());
        return Ok(JsonSerializer.Serialize(ir));
    }

    [HttpGet("compile/{name}/wgsl")]
    public async Task<IActionResult> CompileDevelopModule(string name)
    {
        var shader = GetShader(name);
        if (shader is null)
        {
            return NotFound();
        }
        var code = await shader.EmitWgslCode();
        return Ok(code);
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }


    [HttpGet("wgsl/vertexbufferlayout/{name}")]
    public async Task<IActionResult> GetVertexBufferLayout(string name)
    {
        if (name == nameof(RaymarchingPrimitiveShader))
        {
            var reflection = new RaymarchingPrimitivesShaderReflection();
            return Ok(reflection.GetVertexBufferLayout());
        }
        else if (name == nameof(ReflectionTestShader))
        {
            var reflection = new ReflectionTestShaderReflection();
            return Ok(reflection.GetVertexBufferLayout());
        }
        else if (name == nameof(MandelbrotDistanceShader))
        {
            var reflection = new SampleFragmentShaderReflection();
            return Ok(reflection.GetVertexBufferLayout());
        }
        return NotFound();
    }

    [HttpGet("wgsl/bindgrouplayoutdescriptor/{name}")]
    public async Task<IActionResult> GetBindGroupLayoutDescriptor(string name)
    {
        if (name == nameof(RaymarchingPrimitiveShader))
        {
            var shaderModule = new RaymarchingPrimitiveShader();
            var type = shaderModule.GetType();
            var module = Compiler.Reflect(shaderModule);
            var reflection = new RaymarchingPrimitivesShaderReflection();
            return Ok(reflection.GetBindGroupLayoutDescriptor(module));
        }
        else if (name == nameof(MandelbrotDistanceShader))
        {
            var shaderModule = new MandelbrotDistanceShader();
            var type = shaderModule.GetType();
            var module = Compiler.Reflect(shaderModule);
            var reflection = new SampleFragmentShaderReflection();
            return Ok(reflection.GetBindGroupLayoutDescriptor(module));
        }
        else if (name == nameof(MandelbrotDistanceShader))
        {
            var shaderModule = new MandelbrotDistanceShader();
            var module = Compiler.Reflect(shaderModule);
            var reflection = new SampleFragmentShaderReflection();
            return Ok(reflection.GetBindGroupLayoutDescriptor(module));
        }
        return NotFound();
    }


    [HttpGet("wgsl/bindgrouplayoutdescriptorbuffer/{name}")]
    public async Task<IActionResult> GetBindGroupLayoutDescriptorBuffer(string name)
    {
        if (name == nameof(RaymarchingPrimitiveShader))
        {
            var shaderModule = new RaymarchingPrimitiveShader();
            var type = shaderModule.GetType();
            var module = Compiler.Reflect(shaderModule);
            var reflection = new RaymarchingPrimitivesShaderReflection();
            return Ok(reflection.GetBindGroupLayoutDescriptorBuffer(module));
        }
        else if (name == nameof(MandelbrotDistanceShader))
        {
            var shaderModule = new MandelbrotDistanceShader();
            var type = shaderModule.GetType();
            var module = Compiler.Reflect(shaderModule);
            var reflection = new SampleFragmentShaderReflection();
            return Ok(reflection.GetBindGroupLayoutDescriptorBuffer(module));
        }
        return NotFound();
    }
}

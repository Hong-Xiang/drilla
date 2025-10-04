using DualDrill.CLSL;
using DualDrill.Engine.Shader;
using DualDrill.Server.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace DualDrill.Server.Controllers;

[Route("[controller]")]
public class ILSLController(ILSLDevelopShaderModuleService ShaderModules, SlangService slangService) : Controller
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

    [HttpGet("compile/{name}/{target}")]
    public async Task<IActionResult> CompileDevelopModuleToSlang(string name, string target)
    {
        var shader = GetShader(name);
        if (shader is null)
        {
            return NotFound();
        }

        var targetOption = target.ToLower() switch
        {
            "ir" => CLSLCompileTarget.IR,
            "wgsl" => CLSLCompileTarget.WGSL,
            "slang" => CLSLCompileTarget.SLang,
            _ => throw new NotSupportedException()
        };

        ICLSLCompiler compiler = new CLSLCompiler(new CLSLCompileOption(targetOption));
        var code = compiler.Emit(shader);
        return Ok(code);
    }

    [HttpGet("reflect/{name}")]
    public async Task<IActionResult> Reflect(string name)
    {
        var shader = GetShader(name);
        if (shader is null)
        {
            return NotFound();
        }

        ICLSLCompiler compiler = new CLSLCompiler(new CLSLCompileOption(CLSLCompileTarget.SLang));
        var code = compiler.Emit(shader);
        var json = await slangService.ReflectAsync(code);
        return Ok(JsonSerializer.Deserialize<JsonDocument>(json));
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

    private ICLSLCompiler Compiler => throw new NotImplementedException();

    [HttpGet("wgsl/bindgrouplayoutdescriptor/{name}")]
    public async Task<IActionResult> GetBindGroupLayoutDescriptor(string name)
    {
        if (name == nameof(RaymarchingPrimitiveShader))
        {
            var shaderModule = new RaymarchingPrimitiveShader();
            var type = shaderModule.GetType();
            var module = Compiler.Parse(shaderModule);
            var reflection = new RaymarchingPrimitivesShaderReflection();
            return Ok(reflection.GetBindGroupLayoutDescriptor(module));
        }
        else if (name == nameof(MandelbrotDistanceShader))
        {
            var shaderModule = new MandelbrotDistanceShader();
            var type = shaderModule.GetType();
            var module = Compiler.Parse(shaderModule);
            var reflection = new SampleFragmentShaderReflection();
            return Ok(reflection.GetBindGroupLayoutDescriptor(module));
        }
        else if (name == nameof(MandelbrotDistanceShader))
        {
            var shaderModule = new MandelbrotDistanceShader();
            var module = Compiler.Parse(shaderModule);
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
            var module = Compiler.Parse(shaderModule);
            var reflection = new RaymarchingPrimitivesShaderReflection();
            return Ok(reflection.GetBindGroupLayoutDescriptorBuffer(module));
        }
        else if (name == nameof(MandelbrotDistanceShader))
        {
            var shaderModule = new MandelbrotDistanceShader();
            var type = shaderModule.GetType();
            var module = Compiler.Parse(shaderModule);
            var reflection = new SampleFragmentShaderReflection();
            return Ok(reflection.GetBindGroupLayoutDescriptorBuffer(module));
        }

        return NotFound();
    }
}
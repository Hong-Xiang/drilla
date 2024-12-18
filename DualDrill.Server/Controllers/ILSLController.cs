using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Types;
using DualDrill.Engine.Shader;
using DualDrill.ILSL;
using DualDrill.ILSL.Frontend;
using DualDrill.Server.Services;
using Lokad.ILPack.IL;
using Microsoft.AspNetCore.Mvc;
using System.Numerics;
using System.Reflection;

namespace DualDrill.Server.Controllers;

[Route("[controller]")]
public class ILSLController(ILSLDevelopShaderModuleService ShaderModules) : Controller
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

    [HttpGet("compile/{name}/ir")]
    public IActionResult ParseDevelopModule(string name)
    {
        var shader = GetShader(name);
        if (shader is null)
        {
            return NotFound();
        }
        var ir = ILSL.ILSLCompiler.Parse(shader);
        return Ok(ir);
    }

    [HttpGet("compile/{name}/wgsl")]
    public async Task<IActionResult> CompileDevelopModule(string name)
    {
        var shader = GetShader(name);
        if (shader is null)
        {
            return NotFound();
        }
        //var sm = new MandelbrotDistanceShader();
        //var sm = new MinimumTriangle();
        //var sm = new BasicConditionShader();
        //var ir = ILSL.ILSLCompiler.Parse(sm);
        //var code = await ILSLCompiler.EmitCode(ir);
        var code = await ILSLCompiler.CompileV2(shader);
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
        if (name == nameof(QuadShader))
        {
            var reflection = new QuadShaderReflection();
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
        if (name == nameof(QuadShader))
        {
            var shaderModule = new QuadShader();
            var type = shaderModule.GetType();
            using var bodyParser = new ILSpyMethodParser(new ILSpyOption()
            {
                HotReloadAssemblies = [
                   type.Assembly,
               typeof(ILSLCompiler).Assembly
                ]
            });

            var parser = new CLSLParser(bodyParser);
            var module = parser.ParseShaderModule(shaderModule);
            var reflection = new QuadShaderReflection();
            return Ok(reflection.GetBindGroupLayoutDescriptor(module));
        }
        else if (name == nameof(MandelbrotDistanceShader))
        {
            var shaderModule = new MandelbrotDistanceShader();
            var type = shaderModule.GetType();
            using var bodyParser = new ILSpyMethodParser(new ILSpyOption()
            {
                HotReloadAssemblies = [
                    type.Assembly,
            typeof(ILSLCompiler).Assembly
                ]
            });

            //var parser = new CLSLParser(bodyParser);
            //var module = parser.ParseShaderModule(shaderModule);
            var module = ILSL.ILSLCompiler.Parse(shaderModule);
            var reflection = new SampleFragmentShaderReflection();
            return Ok(reflection.GetBindGroupLayoutDescriptor(module));
        }
        else if (name == nameof(MandelbrotDistanceShader))
        {
            var shaderModule = new MandelbrotDistanceShader();
            var type = shaderModule.GetType();
            using var bodyParser = new ILSpyMethodParser(new ILSpyOption()
            {
                HotReloadAssemblies = [
                    type.Assembly,
            typeof(ILSLCompiler).Assembly
                ]
            });

            var parser = new CLSLParser(bodyParser);
            var module = parser.ParseShaderModule(shaderModule);
            var reflection = new SampleFragmentShaderReflection();
            return Ok(reflection.GetBindGroupLayoutDescriptor(module));
        }
        return NotFound();
    }


    [HttpGet("wgsl/bindgrouplayoutdescriptorbuffer/{name}")]
    public async Task<IActionResult> GetBindGroupLayoutDescriptorBuffer(string name)
    {
        if (name == nameof(QuadShader))
        {
            var shaderModule = new QuadShader();
            var type = shaderModule.GetType();
            using var bodyParser = new ILSpyMethodParser(new ILSpyOption()
            {
                HotReloadAssemblies = [
                   type.Assembly,
               typeof(ILSLCompiler).Assembly
                ]
            });

            var parser = new CLSLParser(bodyParser);
            var module = parser.ParseShaderModule(shaderModule);
            var reflection = new QuadShaderReflection();
            return Ok(reflection.GetBindGroupLayoutDescriptorBuffer(module));
        }
        else if (name == nameof(MandelbrotDistanceShader))
        {
            var shaderModule = new MandelbrotDistanceShader();
            var type = shaderModule.GetType();
            using var bodyParser = new ILSpyMethodParser(new ILSpyOption()
            {
                HotReloadAssemblies = [
                   type.Assembly,
               typeof(ILSLCompiler).Assembly
                ]
            });

            //var parser = new CLSLParser(bodyParser);
            var module = ILSL.ILSLCompiler.Parse(shaderModule);
            var reflection = new SampleFragmentShaderReflection();
            return Ok(reflection.GetBindGroupLayoutDescriptorBuffer(module));
        }
        return NotFound();
    }



    async Task<IActionResult> MethodTargetAction(string moduleName, string methodName,
            Func<ILSpyMethodParser, MethodBase, Task<IActionResult>> next)
    {
        var shaderModule = GetShader(moduleName);
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
        var parser = new ILSpyMethodParser(new ILSpyOption
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
            var module = new ShaderModuleDeclaration([ir]);
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
        new
        {
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
}

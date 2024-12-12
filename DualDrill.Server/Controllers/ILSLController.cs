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
    ISharpShader? GetShaderModule(string name)
    {
        return ShaderModules.ShaderModules[name];
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

    [HttpGet("mothod/parse")]
    public IActionResult ParseDevelopMethod()
    {
        var methodParser = new RelooperMethodParser();
        var parser = new CLSLParser(methodParser);
        var ht = new MandelbrotDistanceShader();

        var ir = ILSL.ILSLCompiler.Parse(ht);
        return Ok(ir);
    }



    [HttpGet("parse")]
    public IActionResult ParseDevelopModule()
    {
        var ht = new MandelbrotDistanceShader();
        var ir = ILSL.ILSLCompiler.Parse(ht);
        return Ok(ir);
    }

    [HttpGet("compile")]
    public async Task<IActionResult> CompileDevelopModule()
    {
        var sm = new MandelbrotDistanceShader();
        //var sm = new BasicConditionShader();
        //var ir = ILSL.ILSLCompiler.Parse(sm);
        //var code = await ILSLCompiler.EmitCode(ir);
        var code = await ILSLCompiler.CompileV2(sm);
        return Ok(code);
    }

    [HttpGet("emit")]
    public async Task<IActionResult> EmitDevelopModule()
    {
        var module = new ShaderModuleDeclaration([
            new FunctionDeclaration("foo", [], new FunctionReturn(ShaderType.I32, []), []){
                Body = new([
                ])
            }
        ]);
        //var ht = new MandelbrotDistanceShader();
        var sm = new BasicConditionShader();
        var ir = ILSL.ILSLCompiler.Parse(sm);
        var code = await ILSLCompiler.EmitCode(ir);
        return Ok(code);
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("wgsl/{name}/expected")]
    public IActionResult ExpectedCode(string name)
    {
        var shaderModule = GetShaderModule(name);
        var a = shaderModule.GetType().GetCustomAttribute<CLSLDevelopExpectedWGPUCodeAttribute>();
        return Ok(a.Code);
    }


    [HttpGet("wgsl/{name}")]
    public async Task<IActionResult> CompileModule(string name)
    {
        if (name == nameof(SimpleUniformShader))
        {
            return Ok(await ILSLCompiler.CompileV2(new SimpleUniformShader()));
        }
        if (name == nameof(MandelbrotDistanceShader))
        {
            return Ok(await ILSLCompiler.CompileV2(new MandelbrotDistanceShader()));
        }
        if (name == nameof(QuadShader))
        {
            return Ok(await ILSLCompiler.CompileV2(new QuadShader()));
        }

        var shaderModule = GetShaderModule(name);
        if (shaderModule is null)
        {
            return NotFound();
        }

        return Ok(await ILSLCompiler.Compile(shaderModule));
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

            var parser = new CLSLParser(bodyParser);
            var module = parser.ParseShaderModule(shaderModule);
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

            var parser = new CLSLParser(bodyParser);
            var module = parser.ParseShaderModule(shaderModule);
            var reflection = new SampleFragmentShaderReflection();
            return Ok(reflection.GetBindGroupLayoutDescriptorBuffer(module));
        }
        return NotFound();
    }



    async Task<IActionResult> MethodTargetAction(string moduleName, string methodName,
            Func<ILSpyMethodParser, MethodBase, Task<IActionResult>> next)
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

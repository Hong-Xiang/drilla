using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Transform;
using Xunit.Abstractions;

namespace DualDrill.CLSL.Test;

public sealed class RuntimeReflectionCompilerE2ETests(ITestOutputHelper Output)
{
    void Dump(string title, ShaderModuleDeclaration<FunctionBody4> module)
    {
        var formatter = new ShaderModuleFormatter();
        Output.WriteLine($"=== {title} ===");
        module.Accept(formatter);
        Output.WriteLine(formatter.Dump());
    }

    string OutputFolder = Path.GetDirectoryName(typeof(RuntimeReflectionCompilerE2ETests).Assembly.Location);

    async Task TestShader(ISharpShader shader, string name)
    {
        var sep = $"\n{new string('-', 10)}\n";
        var context = CompilationContext.Create();
        var parser = new RuntimeReflectionParser(context);
        var module = parser.ParseShaderModule(shader);
        Dump("IR", module);
        //module = module.RunPass(new ParameterWithSemanticBindingToModuleVariablePass());
        module = module.RunPass(new FunctionToOperationPass());
        module = module.RunPass(new RegionParameterToLocalVariablePass());

        //Dump($"After {nameof(ParameterWithSemanticBindingToModuleVariablePass)} IR", module);
        Dump("IR after passes", module);

        var emitter = new SlangEmitter(module);

        var code = emitter.Emit();
        Output.WriteLine("=== SLang ===");
        Output.WriteLine(code);

        var slangPath = Path.Combine(OutputFolder, $"{name}-gen.slang");
        using var f = File.CreateText(slangPath);
        f.WriteLine(code);
        await f.FlushAsync();
        Output.WriteLine($"[Write Slang to file] {slangPath}");

        var slangService = new SlangService();
        await slangService.ValidateAsync(code);

        //var wgsl = await slangService.CompileToWgslAsync(code);
        //Output.WriteLine("=== WGSL ===");
        //Output.WriteLine(wgsl);


        //cfg = cfg.EliminateBlockValueTransfer();
        //Output.WriteLine("=== Remove Outputs ===");
        //Output.WriteLine(await cfg.Dump());
        //Output.WriteLine(sep);

        //var cfgOp = cfg.BasicBlockTransformStatementsToInstructions()
        //               .ReplaceOperationCallsToOperationInstruction();
        //Output.WriteLine("=== Parsed(Op) ===");
        //Output.WriteLine(await cfgOp.Dump());
        //Output.WriteLine(sep);

        //var scf = cfgOp.ToStructuredControlFlowStackModel();
        //Output.WriteLine("=== SCF ===");
        //Output.WriteLine(await scf.Dump());
        //Output.WriteLine(sep);
        //scf = scf.Simplify();
        //Output.WriteLine("=== SCF (Simplified) ===");
        //Output.WriteLine(await scf.Dump());
        //Output.WriteLine(sep);
        //var ast = scf.ToAbstractSyntaxTreeFunctionBody();
        //Output.WriteLine("=== AST ===");
        //Output.WriteLine(await ast.Dump());

        //Output.WriteLine(sep);

        //var astS = ast.Simplify();
        //Output.WriteLine("=== AST(Simplified) ===");
        //Output.WriteLine(await astS.Dump());
        //Output.WriteLine(sep);

        //var code = await astS.EmitWgslCode();
        //Output.WriteLine("=== WGSL ===");
        //Output.WriteLine(code);
        //Output.WriteLine(sep);
    }

    [Fact]
    public async Task MinimumTriangleShaderShouldWork()
    {
        var shader = new ShaderModule.MinimumHelloTriangleShaderModule();
        await TestShader(shader, "triangle");
    }

    [Fact]
    public async Task AdHocDevelopTest()
    {
        var shader = new ShaderModule.DevelopShaderModule();
        await TestShader(shader, "adhoc-develop");
    }

    [Fact]
    public async Task DevelopTestShaderModuleShouldWork()
    {
        var shader = new ShaderModule.DevelopTestShaderModule();
        await TestShader(shader, "develop-test");
    }

    [Fact]
    public async Task MandelbrotDistanceShaderModuleShouldWork()
    {
        var shader = new ShaderModule.MandelbrotDistanceShaderModule();
        await TestShader(shader, "mandelbrot");
    }

    [Fact]
    public async Task SimpleUniformShaderShouldWork()
    {
        var shader = new ShaderModule.SimpleStructUniformShaderModule();
        await TestShader(shader, "simple-uniform");
    }

    [Fact]
    public async Task RayMartching()
    {
        var shader = new ShaderModule.RaymarchingPrimitiveShader();
        await TestShader(shader, "raymartching");
    }
}
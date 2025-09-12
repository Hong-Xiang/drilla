using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Transform;
using DualDrill.CLSL.Transform;
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
    void TestShader(ISharpShader shader, string name)
    {
        var sep = $"\n{new string('-', 10)}\n";
        var module = shader.Parse4();
        Dump("IR", module);
        //module = module.RunPass(new ParameterWithSemanticBindingToModuleVariablePass());
        //module = module.RunPass(new FunctionToOperationPass());
        module = module.RunPass(new RegionParameterToLocalVariablePass());

        //Dump($"After {nameof(ParameterWithSemanticBindingToModuleVariablePass)} IR", module);
        Dump("IR after passes", module);

        var emitter = new WGSLEmitter(module);

        var code = emitter.Emit();
        Output.WriteLine("=== WGSL ===");
        Output.WriteLine(code);

        using var f = File.CreateText($"D:\\Code\\DualDrillEngine\\DualDrill.CLSL.Test\\ShaderModule\\{name}-gen.wgsl");
        f.WriteLine(code);

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
        TestShader(shader, "triangle");
    }

    [Fact]
    public async Task AdHocDevelopTest()
    {
        var shader = new ShaderModule.DevelopShaderModule();
        TestShader(shader, "adhoc-develop");
    }

    [Fact]
    public async Task DevelopTestShaderModuleShouldWork()
    {
        var shader = new ShaderModule.DevelopTestShaderModule();
        TestShader(shader, "develop-test");
    }

    [Fact]
    public async Task MandelbrotDistanceShaderModuleShouldWork()
    {
        var shader = new ShaderModule.MandelbrotDistanceShaderModule();
        TestShader(shader, "mandelbrot");
    }

    [Fact]
    public async Task SimpleUniformShaderShouldWork()
    {
        var shader = new ShaderModule.SimpleStructUniformShaderModule();
        TestShader(shader, "simple-uniform");
    }

    [Fact]
    public async Task RayMartching()
    {
        var shader = new ShaderModule.RaymarchingPrimitiveShader();
        TestShader(shader, "raymartching");
    }
}
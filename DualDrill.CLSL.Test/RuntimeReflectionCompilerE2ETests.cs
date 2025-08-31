using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Language;
using Xunit.Abstractions;

namespace DualDrill.CLSL.Test;

public sealed class RuntimeReflectionCompilerE2ETests(ITestOutputHelper Output)
{
    void TestShader(ISharpShader shader)
    {
        var sep = $"\n{new string('-', 10)}\n";
        var module = shader.Parse4();

        var formatter = new ShaderModuleFormatter();
        Output.WriteLine("=== IR ===");
        module.Accept(formatter);
        Output.WriteLine(formatter.Dump());

        var emitter = new SPIRVEmitter();

        var code = emitter.Emit(module);
        Output.WriteLine("=== SPIRV ===");
        Output.WriteLine(code);


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
        TestShader(shader);
    }

    [Fact]
    public async Task AdHocDevelopTest()
    {
        var shader = new ShaderModule.DevelopShaderModule();
        TestShader(shader);
    }

    [Fact]
    public async Task DevelopTestShaderModuleShouldWork()
    {
        var shader = new ShaderModule.DevelopTestShaderModule();
        TestShader(shader);
    }

    [Fact]
    public async Task MandelbrotDistanceShaderModuleShouldWork()
    {
        var shader = new ShaderModule.MandelbrotDistanceShaderModule();
        TestShader(shader);
    }

    [Fact]
    public async Task SimpleUniformShaderShouldWork()
    {
        var shader = new ShaderModule.SimpleStructUniformShaderModule();
        TestShader(shader);
    }
}
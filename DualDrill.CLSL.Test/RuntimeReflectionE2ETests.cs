using DualDrill.CLSL.Compiler;
using Xunit.Abstractions;

namespace DualDrill.CLSL.Test;

public sealed class RuntimeReflectionE2ETests(ITestOutputHelper Output)
{

    async Task TestShader(ISharpShader shader)
    {
        var sep = $"\n{new string('-', 10)}\n";
        var moduleStackIR = shader.Parse();
        Output.WriteLine("=== Parsed ===");
        Output.WriteLine(await moduleStackIR.Dump());
        Output.WriteLine(sep);
        var moduleStackIROp = moduleStackIR.ReplaceOperationCallsToOperationInstruction();
        Output.WriteLine("=== Parsed(Op) ===");
        Output.WriteLine(await moduleStackIROp.Dump());
        Output.WriteLine(sep);
        var cfg = moduleStackIROp.ToControlFlowGraph();
        Output.WriteLine("=== CFG ===");
        Output.WriteLine(await cfg.Dump());
        Output.WriteLine(sep);
        var scf = cfg.ToStructuredControlFlowStackModel();
        Output.WriteLine("=== SCF ===");
        Output.WriteLine(await scf.Dump());
        Output.WriteLine(sep);
        var ast = scf.ToAbstractSyntaxTreeFunctionBody();
        Output.WriteLine("=== AST ===");
        Output.WriteLine(await ast.Dump());

        Output.WriteLine(sep);

        var astS = ast.Simplify();
        Output.WriteLine("=== AST(Simplified) ===");
        Output.WriteLine(await astS.Dump());
        Output.WriteLine(sep);

        var code = await astS.EmitWgslCode();
        Output.WriteLine("=== WGSL ===");
        Output.WriteLine(code);
        Output.WriteLine(sep);
    }

    [Fact]
    public async Task MinimumTriangleShaderShouldWork()
    {
        var shader = new ShaderModule.MinimumHelloTriangleShaderModule();
        await TestShader(shader);
    }

    [Fact]
    public async Task DevelopTestShaderModuleShouldWork()
    {
        var shader = new ShaderModule.DevelopTestShaderModule();
        await TestShader(shader);
    }

    [Fact]
    public async Task MandelbrotDistanceShaderModuleShouldWork()
    {
        var shader = new ShaderModule.MandelbrotDistanceShaderModule();
        await TestShader(shader);

    }

    [Fact]
    public async Task SimpleUniformShaderShouldWork()
    {
        var shader = new ShaderModule.SimpleStructUniformShaderModule();
        await TestShader(shader);
    }
}

using DualDrill.CLSL.Compiler;
using Xunit.Abstractions;

namespace DualDrill.CLSL.Test;

public sealed class RuntimeReflectionE2ETests(ITestOutputHelper Output)
{

    async Task TestShader(ISharpShader shader)
    {
        var moduleStackIR = shader.Parse();
        Output.WriteLine("Parsed:");
        Output.WriteLine(moduleStackIR.Dump());
        var moduleStackIROp = moduleStackIR.ReplaceOperationCallsToOperationInstruction();
        Output.WriteLine("Parsed(Op):");
        Output.WriteLine(moduleStackIROp.Dump());
        var cfg = moduleStackIROp.ToControlFlowGraph();
        Output.WriteLine("CFG:");
        Output.WriteLine(cfg.Dump());
        var scf = cfg.ToStructuredControlFlowStackModel();
        Output.WriteLine("SCF:");
        Output.WriteLine(scf.Dump());
        var ast = scf.ToAbstractSyntaxTreeFunctionBody();
        Output.WriteLine("AST:");
        Output.WriteLine(ast.Dump());
        var code = await ast.EmitWgslCode();
        Output.WriteLine("WGSL:");
        Output.WriteLine(code);
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

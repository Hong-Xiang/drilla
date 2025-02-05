using DualDrill.CLSL.Compiler;
using Xunit.Abstractions;

namespace DualDrill.CLSL.Test;

public sealed class RuntimeReflectionE2ETests(ITestOutputHelper Output)
{
    [Fact]
    public async Task MinimumTriangleShaderShouldWork()
    {
        var shader = new ShaderModule.MinimumHelloTriangleShaderModule();
        var code = await shader.EmitWgslCode();
        Output.WriteLine("generated code:");
        Output.WriteLine(code);

    }

    [Fact]
    public async Task DevelopTestShaderModuleShouldWork()
    {
        var shader = new ShaderModule.DevelopTestShaderModule();
        var code = await shader.EmitWgslCode();
        Output.WriteLine("generated code:");
        Output.WriteLine(code);
    }

    [Fact]
    public async Task MandelbrotDistanceShaderModuleShouldWork()
    {
        var shader = new ShaderModule.MandelbrotDistanceShaderModule();
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
        Output.WriteLine("generated code:");
        Output.WriteLine(code);
    }
}

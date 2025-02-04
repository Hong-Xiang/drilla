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
        var code = await shader.EmitWgslCode();
        Output.WriteLine("generated code:");
        Output.WriteLine(code);
    }
}

using Xunit.Abstractions;

namespace DualDrill.CLSL.Test;

public sealed class RuntimeReflectionE2ETests(ITestOutputHelper Output)
{
    [Fact]
    public async Task MinimumTriangleShaderShouldWork()
    {
        var shader = new MinimumHelloTriangleShaderModule();
        var code = await shader.EmitWgslCode();
        Output.WriteLine("generated code:");
        Output.WriteLine(code);
    }
}

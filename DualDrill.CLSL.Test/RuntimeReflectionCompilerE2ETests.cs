using System.Diagnostics;
using System.Text.Json;
using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Transform;
using Xunit.Abstractions;

namespace DualDrill.CLSL.Test;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SlangProcessTestCollection
{
    public const string Name = "Slang process";
}

[Collection(SlangProcessTestCollection.Name)]
public sealed class RuntimeReflectionCompilerE2ETests(ITestOutputHelper Output)
{
    void Dump(string title, ShaderModuleDeclaration<FunctionBody4> module)
    {
        var formatter = new ShaderModuleFormatter();
        Output.WriteLine($"=== {title} ===");
        module.Accept(formatter);
        Output.WriteLine(formatter.Dump());
    }

    string OutputFolder { get; } =
        Path.GetDirectoryName(typeof(RuntimeReflectionCompilerE2ETests).Assembly.Location)
        ?? throw new InvalidOperationException("Test assembly has no output directory");

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
        var compilation = await AssertPublicWgslCompilation(shader);
        using var reflection = compilation.Reflection;
        Assert.Contains("@builtin(vertex_index)", compilation.Wgsl);
        Assert.Contains("@builtin(position)", compilation.Wgsl);
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
        var compilation = await AssertPublicWgslCompilation(shader);
        using var reflection = compilation.Reflection;
        var parameter = Assert.Single(reflection.RootElement
            .GetProperty("parameters")
            .EnumerateArray());

        var binding = parameter.GetProperty("binding");
        Assert.Equal("descriptorTableSlot", binding.GetProperty("kind").GetString());
        Assert.Equal(0, binding.GetProperty("index").GetInt32());

        var type = parameter.GetProperty("type");
        Assert.Equal("constantBuffer", type.GetProperty("kind").GetString());
        Assert.Equal(
            "struct",
            type.GetProperty("elementType").GetProperty("kind").GetString());

        Assert.Contains("@group(0)", compilation.Wgsl);
        Assert.Contains("@binding(0)", compilation.Wgsl);
        Assert.Contains("var<uniform>", compilation.Wgsl);
        Assert.Contains("color", compilation.Wgsl);
        Assert.Contains("scale", compilation.Wgsl);
        Assert.Contains("offset", compilation.Wgsl);
    }

    [Fact]
    public async Task RayMartching()
    {
        var shader = new ShaderModule.RaymarchingPrimitiveShader();
        await TestShader(shader, "raymartching");
    }

    [Fact]
    public async Task SlangDiagnosticsRemainVisible()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new SlangService().ValidateAsync("this is not valid Slang"));

        Assert.Contains("slangc validation failed", exception.Message);
        var stderr = exception.Message.Split("\nStandard error:\n", 2);
        Assert.Equal(2, stderr.Length);
        Assert.False(string.IsNullOrWhiteSpace(stderr[1]));
    }

    [Fact]
    public async Task CancellationReapsSlangAndCleansItsSource()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(directory);
        var compilerPath = Path.Combine(directory, "slangc");
        var argumentsPath = Path.Combine(directory, "arguments");
        var pidPath = Path.Combine(directory, "pid");
        await File.WriteAllTextAsync(
            compilerPath,
            """
            #!/bin/sh
            printf '%s\n' "$$" > "$SLANG_TEST_PID_PATH"
            printf '%s\n' "$@" > "$SLANG_TEST_ARGUMENTS_PATH"
            sleep 30
            """);
        File.SetUnixFileMode(
            compilerPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var previousPath = Environment.GetEnvironmentVariable("PATH")
            ?? throw new InvalidOperationException("PATH is not set");
        Environment.SetEnvironmentVariable("PATH", $"{directory}{Path.PathSeparator}{previousPath}");
        Environment.SetEnvironmentVariable("SLANG_TEST_ARGUMENTS_PATH", argumentsPath);
        Environment.SetEnvironmentVariable("SLANG_TEST_PID_PATH", pidPath);

        try
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var validation = new SlangService().ValidateAsync("validity is controlled by the test", cancellation.Token);
            await WaitForFileAsync(argumentsPath, cancellation.Token);
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => validation);

            var arguments = await File.ReadAllLinesAsync(argumentsPath);
            Assert.Equal(4, arguments.Length);
            Assert.False(File.Exists(arguments[0]));
            Assert.Equal("-target", arguments[1]);
            Assert.Equal("wgsl", arguments[2]);
            Assert.Equal("-no-codegen", arguments[3]);

            var pid = int.Parse(await File.ReadAllTextAsync(pidPath));
            Assert.Throws<ArgumentException>(() => Process.GetProcessById(pid));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", previousPath);
            Environment.SetEnvironmentVariable("SLANG_TEST_ARGUMENTS_PATH", null);
            Environment.SetEnvironmentVariable("SLANG_TEST_PID_PATH", null);
            Directory.Delete(directory, recursive: true);
        }
    }

    static async Task WaitForFileAsync(string path, CancellationToken cancellation)
    {
        while (!File.Exists(path))
            await Task.Delay(TimeSpan.FromMilliseconds(10), cancellation);
    }

    async Task<(JsonDocument Reflection, string Wgsl)> AssertPublicWgslCompilation(
        ISharpShader shader)
    {
        var slangCompiler = new CLSLCompiler(new(CLSLCompileTarget.SLang));
        var slang = slangCompiler.Emit(shader);
        var reflectionJson = await new SlangService().ReflectAsync(slang);
        var reflection = JsonDocument.Parse(reflectionJson);

        var entryPoints = reflection.RootElement
            .GetProperty("entryPoints")
            .EnumerateArray()
            .ToDictionary(
                entryPoint => entryPoint.GetProperty("name").GetString()
                    ?? throw new InvalidDataException("Slang reflection entry point has no name"),
                entryPoint => entryPoint.GetProperty("stage").GetString()
                    ?? throw new InvalidDataException("Slang reflection entry point has no stage"));
        Assert.Equal(2, entryPoints.Count);
        Assert.Equal("vertex", entryPoints["vs"]);
        Assert.Equal("fragment", entryPoints["fs"]);

        var wgslCompiler = new CLSLCompiler(new(CLSLCompileTarget.WGSL));
        var wgsl = wgslCompiler.Emit(shader);
        Output.WriteLine("=== WGSL ===");
        Output.WriteLine(wgsl);

        Assert.False(string.IsNullOrWhiteSpace(wgsl));
        Assert.Contains("@vertex", wgsl);
        Assert.Contains("@fragment", wgsl);
        Assert.Contains("fn vs", wgsl);
        Assert.Contains("fn fs", wgsl);

        return (reflection, wgsl);
    }
}
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json;
using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Transform;
using DualDrill.Mathematics;
using Xunit.Abstractions;
using static DualDrill.Mathematics.DMath;

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
    public async Task RayMarchingCompilesThroughPublicWgslApi()
    {
        var shader = new ShaderModule.RaymarchingPrimitiveShader();
        var compilation = await AssertPublicWgslCompilation(shader);
        using var reflection = compilation.Reflection;
        var parameters = reflection.RootElement
            .GetProperty("parameters")
            .EnumerateArray()
            .ToArray();

        Assert.Equal(2, parameters.Length);
        Assert.Equal(
            [0, 1],
            parameters
                .Select(parameter => parameter.GetProperty("binding").GetProperty("index").GetInt32())
                .Order());
        Assert.All(
            parameters,
            parameter => Assert.Equal(
                "descriptorTableSlot",
                parameter.GetProperty("binding").GetProperty("kind").GetString()));
        Assert.Contains("@group(0)", compilation.Wgsl);
        Assert.Contains("@binding(0)", compilation.Wgsl);
        Assert.Contains("@binding(1)", compilation.Wgsl);
        Assert.Contains("var<uniform>", compilation.Wgsl);
        Assert.Contains("iResolution", compilation.Wgsl);
        Assert.Contains("iTime", compilation.Wgsl);
    }

    [Fact]
    public void MultipleReturnHelperUsesConfigurationSpecificCilTopology()
    {
        var configuration = typeof(RuntimeReflectionCompilerE2ETests).Assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration;
        var method = typeof(MultipleReturnShader).GetMethod(
            "Select",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Multiple-return helper method was not found");
        var actualMethodBody = new MethodBodyAnalysisModel(method);
        var controlFlowGraph = actualMethodBody.ControlFlowGraph;
        var labels = controlFlowGraph.Labels().ToArray();
        var conditional = Assert.Single(
            labels,
            label => controlFlowGraph.GetSucc(label).Count() == 2);
        var branchTargets = controlFlowGraph.GetSucc(conditional).ToArray();
        var postDominators = controlFlowGraph.ControlFlowAnalysis().PostDominatorTree;

        switch (configuration)
        {
            case "Debug":
                {
                    Assert.Equal(4, labels.Length);
                    var sharedReturn = Assert.Single(
                        labels,
                        label => !controlFlowGraph.GetSucc(label).Any());

                    Assert.Equal(2, branchTargets.Length);
                    Assert.All(
                        branchTargets,
                        target =>
                        {
                            Assert.Equal(
                                sharedReturn,
                                Assert.IsType<UnconditionalSuccessor>(
                                    controlFlowGraph.Successor(target)).Target);
                            Assert.Equal(sharedReturn, postDominators.ImmediatePostDominator(target));
                        });
                    Assert.Equal(sharedReturn, postDominators.ImmediatePostDominator(conditional));
                    Assert.Null(postDominators.ImmediatePostDominator(sharedReturn));
                    break;
                }
            case "Release":
                {
                    Assert.Equal(3, labels.Length);
                    var terminalReturns = labels
                        .Where(label => !controlFlowGraph.GetSucc(label).Any())
                        .ToArray();

                    Assert.Equal(2, terminalReturns.Length);
                    Assert.Equal(2, branchTargets.Intersect(terminalReturns).Count());
                    Assert.Null(postDominators.ImmediatePostDominator(conditional));
                    Assert.All(
                        terminalReturns,
                        terminal => Assert.Null(postDominators.ImmediatePostDominator(terminal)));
                    break;
                }
            default:
                throw new InvalidOperationException($"Unexpected build configuration: {configuration}");
        }
    }

    [Fact]
    public async Task MultipleReturnShaderCompilesThroughPublicSlangAndWgslApis()
    {
        var compilation = await AssertPublicWgslCompilation(new MultipleReturnShader());
        using var reflection = compilation.Reflection;

        Assert.Contains(": vec4<f32> = a;", compilation.Slang);
        Assert.Contains(": vec4<f32> = b;", compilation.Slang);
        Assert.Contains("a_0;", compilation.Wgsl);
        Assert.Contains("b_0;", compilation.Wgsl);

        var configuration = typeof(RuntimeReflectionCompilerE2ETests).Assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration;
        if (configuration == "Release")
        {
            Assert.Matches(
                @"let (?<value>\S+) : vec4<f32> = a;\s*return \k<value>;",
                compilation.Slang);
            Assert.Matches(
                @"let (?<value>\S+) : vec4<f32> = b;\s*return \k<value>;",
                compilation.Slang);
        }
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

        await WithFakeSlangAsync(
            """
            #!/bin/sh
            printf '%s\n' "$$" > "$SLANG_TEST_DIRECTORY/pid"
            printf '%s\n' "$@" > "$SLANG_TEST_DIRECTORY/arguments"
            touch "$SLANG_TEST_DIRECTORY/ready"
            sleep 30
            """,
            async directory =>
            {
                var argumentsPath = Path.Combine(directory, "arguments");
                using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var validation = new SlangService().ValidateAsync(
                    "validity is controlled by the test",
                    cancellation.Token);
                await WaitForFileAsync(Path.Combine(directory, "ready"), cancellation.Token);
                cancellation.Cancel();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => validation);

                var arguments = await File.ReadAllLinesAsync(argumentsPath);
                Assert.Equal(4, arguments.Length);
                Assert.Contains("slang test ", arguments[0]);
                Assert.False(File.Exists(arguments[0]));
                Assert.Equal("-target", arguments[1]);
                Assert.Equal("wgsl", arguments[2]);
                Assert.Equal("-no-codegen", arguments[3]);

                var pid = int.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "pid")));
                Assert.Throws<ArgumentException>(() => Process.GetProcessById(pid));
            });
    }

    [Fact]
    public async Task ReflectionAndWgslOutputsAreCleaned()
    {
        if (!OperatingSystem.IsLinux())
            return;

        await WithFakeSlangAsync(
            """
            #!/bin/sh
            if [ "$4" = "-no-codegen" ]; then
                printf '%s\n' "$1" > "$SLANG_TEST_DIRECTORY/reflection-source"
                printf '%s\n' "$6" > "$SLANG_TEST_DIRECTORY/reflection-output"
                printf '{}\n' > "$6"
            else
                printf '%s\n' "$1" > "$SLANG_TEST_DIRECTORY/wgsl-source"
                printf '%s\n' "$5" > "$SLANG_TEST_DIRECTORY/wgsl-output"
                printf '@compute @workgroup_size(1) fn main() {}\n' > "$5"
            fi
            """,
            async directory =>
            {
                var service = new SlangService();
                Assert.Equal("{}", (await service.ReflectAsync("reflection source")).Trim());
                Assert.Contains("@compute", await service.CompileToWgslAsync("WGSL source"));

                foreach (var marker in new[]
                {
                    "reflection-source",
                    "reflection-output",
                    "wgsl-source",
                    "wgsl-output"
                })
                {
                    var temporaryPath = (await File.ReadAllTextAsync(
                        Path.Combine(directory, marker))).Trim();
                    Assert.False(File.Exists(temporaryPath));
                }
            });
    }

    static async Task WaitForFileAsync(string path, CancellationToken cancellation)
    {
        while (!File.Exists(path))
            await Task.Delay(TimeSpan.FromMilliseconds(10), cancellation);
    }

    [SupportedOSPlatform("linux")]
    static async Task WithFakeSlangAsync(string script, Func<string, Task> test)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"slang test {Path.GetRandomFileName()}");
        Directory.CreateDirectory(directory);
        var compilerPath = Path.Combine(directory, "slangc");
        await File.WriteAllTextAsync(compilerPath, script);
        File.SetUnixFileMode(
            compilerPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var previousPath = Environment.GetEnvironmentVariable("PATH")
            ?? throw new InvalidOperationException("PATH is not set");
        var previousTestDirectory = Environment.GetEnvironmentVariable("SLANG_TEST_DIRECTORY");
        var previousTempDirectory = Environment.GetEnvironmentVariable("TMPDIR");
        Environment.SetEnvironmentVariable("PATH", $"{directory}{Path.PathSeparator}{previousPath}");
        Environment.SetEnvironmentVariable("SLANG_TEST_DIRECTORY", directory);
        Environment.SetEnvironmentVariable("TMPDIR", directory);

        try
        {
            await test(directory);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", previousPath);
            Environment.SetEnvironmentVariable("SLANG_TEST_DIRECTORY", previousTestDirectory);
            Environment.SetEnvironmentVariable("TMPDIR", previousTempDirectory);
            Directory.Delete(directory, recursive: true);
        }
    }

    async Task<(JsonDocument Reflection, string Slang, string Wgsl)> AssertPublicWgslCompilation(
        ISharpShader shader)
    {
        var slangCompiler = new CLSLCompiler(new(CLSLCompileTarget.SLang));
        var slang = slangCompiler.Emit(shader);
        Output.WriteLine("=== SLang ===");
        Output.WriteLine(slang);
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

        return (reflection, slang, wgsl);
    }

    private sealed class MultipleReturnShader : ISharpShader
    {
        [Vertex]
        [return: Builtin(BuiltinBinding.position)]
        public static vec4f32 vs() => vec4(0.0f, 0.0f, 0.0f, 1.0f);

        [Fragment]
        [return: Location(0)]
        public static vec4f32 fs([Builtin(BuiltinBinding.position)] vec4f32 position) =>
            Select(
                position.x,
                vec4(0.125f, 0.25f, 0.375f, 1.0f),
                vec4(0.875f, 0.75f, 0.625f, 1.0f));

        [ShaderMethod]
        private static vec4f32 Select(float condition, vec4f32 a, vec4f32 b)
        {
            if (condition > 0.0f)
                return a;

            return b;
        }
    }
}
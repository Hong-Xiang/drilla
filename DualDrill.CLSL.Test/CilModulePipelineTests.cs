using System.Reflection;
using System.Reflection.Emit;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Test.ShaderModule;

namespace DualDrill.CLSL.Test;

public sealed class CilModulePipelineTests
{
    [Fact]
    public void RawModuleFreezesRecursiveReferenceTypeAndMemberClosure()
    {
        var method = GetMethod(nameof(ReadCycle));
        var module = CompilerTestPipeline.ParseRaw(method);
        var body = CompilerTestPipeline.RawBody(module, method);
        var leftField = typeof(LeftNode).GetField(nameof(LeftNode.Right))
                        ?? throw new InvalidOperationException("LeftNode.Right was not found.");
        var rightField = typeof(RightNode).GetField(nameof(RightNode.Left))
                         ?? throw new InvalidOperationException("RightNode.Left was not found.");

        Assert.NotNull(body.Symbols[typeof(LeftNode)]);
        Assert.NotNull(body.Symbols[typeof(RightNode)]);
        Assert.NotNull(body.Symbols[leftField]);
        Assert.NotNull(body.Symbols[rightField]);
    }

    [Fact]
    public void UnknownBodylessExternalMethodIsNotAnImplicitBuiltin()
    {
        var method = ExternalCallFixture();
        var parser = new RuntimeReflectionParser();

        var exception = Assert.Throws<NotSupportedException>(() => parser.ParseMethod(method));

        Assert.Contains("IL_", exception.Message);
        Assert.Contains(nameof(IDisposable.Dispose), exception.InnerException?.Message);
        Assert.Throws<InvalidOperationException>(() => parser.ParseMethod(GetMethod(nameof(Identity))));
    }

    [Fact]
    public void PublishedRawModuleRemainsUnchangedAcrossAllLaterPasses()
    {
        var method = GetMethod(nameof(Identity));
        var rawModule = CompilerTestPipeline.ParseRaw(method);
        var rawBody = CompilerTestPipeline.RawBody(rawModule, method);
        var instructions = rawBody.Code.Instructions.ToArray();
        var declarations = rawModule.Declarations.ToArray();

        var pre = CilPreStackPass.Run(rawModule);
        var controlFlow = CilControlFlowPass.Run(pre);
        var values = CilStackToValuePass.Run(controlFlow);
        _ = CilRegionPass.Run(values);

        Assert.Equal(instructions, rawBody.Code.Instructions);
        Assert.Equal(declarations, rawModule.Declarations);
        Assert.All(rawModule.FunctionDefinitions.Values, body => Assert.IsType<RawCilFunctionBody>(body));
    }

    [Fact]
    public void PublishedSymbolSnapshotDoesNotGrowWhenParserIsReused()
    {
        var parser = new RuntimeReflectionParser();
        var firstMethod = GetMethod(nameof(Identity));
        var laterMethod = GetMethod(nameof(Increment));
        var first = CompilerTestPipeline.RawBody(parser.ParseMethod(firstMethod), firstMethod);

        Assert.Null(first.Symbols[Symbol.Function(laterMethod)]);
        var laterModule = parser.ParseMethod(laterMethod);

        Assert.Null(first.Symbols[Symbol.Function(laterMethod)]);
        Assert.NotNull(CompilerTestPipeline.RawBody(laterModule, laterMethod)
                                                   .Symbols[Symbol.Function(laterMethod)]);
    }

    [Fact]
    public void EveryPublishedPipelineStageHasReadableModuleOutput()
    {
        var method = GetMethod(nameof(Identity));
        var raw = CompilerTestPipeline.ParseRaw(method);
        var pre = CilPreStackPass.Run(raw);
        var controlFlow = CilControlFlowPass.Run(pre);
        var values = CilStackToValuePass.Run(controlFlow);

        Assert.Contains("linear-cil raw", Format(raw));
        Assert.Contains("linear-cil pre-annotated reachable", Format(pre));
        Assert.Contains("reachable-cil-cfg", Format(controlFlow));
        Assert.Contains("flat-value-cfg", Format(values));
    }

    [Fact]
    public void PublicParseAndCompileExposeDifferentTypedBoundaries()
    {
        var compiler = new CLSLCompiler(new(CLSLCompileTarget.IR));
        var shader = new MinimumHelloTriangleShaderModule();

        ShaderModuleDeclaration<RawCilFunctionBody> raw = compiler.Parse(shader);
        ShaderModuleDeclaration<FunctionBody4> compiled = compiler.Compile(raw);

        Assert.NotEmpty(raw.FunctionDefinitions);
        Assert.NotEmpty(compiled.FunctionDefinitions);
    }

    private static string Format<TBody>(ShaderModuleDeclaration<TBody> module)
        where TBody : DualDrill.CLSL.Language.FunctionBody.IFunctionBody
    {
        var formatter = new ShaderModuleFormatter<TBody>();
        module.Accept(formatter);
        return formatter.Dump();
    }

    private static int Identity(int value) => value;
    private static int Increment(int value) => value + 1;

    private static int ReadCycle(LeftNode value) => value.Right.Value;

    private sealed class LeftNode
    {
        public required RightNode Right;
    }

    private sealed class RightNode
    {
        public LeftNode? Left;
        public int Value;
    }

    private static MethodInfo GetMethod(string name) =>
        typeof(CilModulePipelineTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"{name} fixture was not found.");

    private static MethodInfo ExternalCallFixture()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("ExternalCallFixture"),
            AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule("ExternalCallFixture").DefineType(
            "ExternalCallFixture",
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
        var method = type.DefineMethod(
            "DeadExternalCall",
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(int),
            Type.EmptyTypes);
        var dispose = typeof(IDisposable).GetMethod(nameof(IDisposable.Dispose))
                      ?? throw new InvalidOperationException("IDisposable.Dispose was not found.");
        var il = method.GetILGenerator();
        var live = il.DefineLabel();
        il.Emit(OpCodes.Br, live);
        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Callvirt, dispose);
        il.MarkLabel(live);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Ret);
        return (type.CreateType() ?? throw new InvalidOperationException("Fixture type creation failed."))
            .GetMethod("DeadExternalCall")
            ?? throw new InvalidOperationException("Fixture method was not found.");
    }
}

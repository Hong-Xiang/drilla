using System.Reflection;
using System.Reflection.Emit;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
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
        var facts = CilBlockControlFactsPass.Run(values);
        _ = CilRegionPass.Run(facts);

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
    public void PublishedSymbolSnapshotDoesNotObserveMutableParentChanges()
    {
        var parent = CompilationContext.Create();
        var parser = new RuntimeReflectionParser(new CompilationContext(parent));
        var method = GetMethod(nameof(Identity));
        var body = CompilerTestPipeline.RawBody(parser.ParseMethod(method), method);

        parent.AddType(typeof(ParentMutationType), new OpaqueType(typeof(ParentMutationType)));

        Assert.Null(body.Symbols[typeof(ParentMutationType)]);
    }

    [Fact]
    public void UnusedReferenceLocalContributesItsCompleteTypeClosure()
    {
        var method = UnusedReferenceLocalFixture();
        var body = CompilerTestPipeline.RawBody(CompilerTestPipeline.ParseRaw(method), method);

        Assert.NotNull(body.Symbols[typeof(ReferenceContainer)]);
        Assert.NotNull(body.Symbols[typeof(ReferenceLeaf)]);
        Assert.NotNull(body.Symbols[ReferenceContainerLeafField()]);
    }

    [Fact]
    public void StaticMethodOwnerContributesItsTypeClosure()
    {
        var method = typeof(StaticOwner).GetMethod(nameof(StaticOwner.Entry))
                     ?? throw new InvalidOperationException("Static owner entry was not found.");
        var module = CompilerTestPipeline.ParseRaw(method);
        var body = CompilerTestPipeline.RawBody(module, method);

        Assert.NotNull(body.Symbols[typeof(StaticOwner)]);
        Assert.NotNull(body.Symbols[typeof(ReferenceContainer)]);
        Assert.Single(module.FunctionDefinitions);
    }

    [Fact]
    public void DerivedStaticOwnerIncludesBaseTypeAndInheritedLayoutFields()
    {
        var method = typeof(DerivedStaticOwner).GetMethod(nameof(DerivedStaticOwner.Entry))
                     ?? throw new InvalidOperationException("Derived owner entry was not found.");
        var body = CompilerTestPipeline.RawBody(CompilerTestPipeline.ParseRaw(method), method);
        var inherited = typeof(BaseOwner).GetField(
            "Inherited",
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                        ?? throw new InvalidOperationException("Inherited field was not found.");

        Assert.NotNull(body.Symbols[typeof(BaseOwner)]);
        Assert.NotNull(body.Symbols[typeof(ReferenceLeaf)]);
        Assert.NotNull(body.Symbols[inherited]);
    }

    [Fact]
    public void UnusedModuleVariableContributesItsCompleteTypeClosure()
    {
        var module = new RuntimeReflectionParser().ParseShaderModule(new UnusedModuleVariableShader());
        var body = Assert.Single(module.FunctionDefinitions.Values);

        Assert.NotNull(body.Symbols[typeof(ReferenceContainer)]);
        Assert.NotNull(body.Symbols[typeof(ReferenceLeaf)]);
        Assert.Contains(module.Declarations.OfType<VariableDeclaration>(),
            declaration => declaration.Name == nameof(UnusedModuleVariableShader.Value));
    }

    [Fact]
    public void SelfReturningStructPropertyUsesTheRegisteredPlaceholder()
    {
        var type = Assert.IsType<StructureType>(
            new RuntimeReflectionParser().ParseType(typeof(SelfReturningStruct)));
        var member = Assert.Single(type.Declaration.Members,
            declaration => declaration.Name == nameof(SelfReturningStruct.Self));

        Assert.Same(type, member.Type);
    }

    [Fact]
    public unsafe void FunctionPointerSignatureContributesNestedReferenceTypes()
    {
        var method = GetMethod(nameof(FunctionPointerSignature));
        var body = CompilerTestPipeline.RawBody(CompilerTestPipeline.ParseRaw(method), method);
        var functionPointer = Assert.Single(method.GetParameters()).ParameterType;
        var field = typeof(FunctionPointerPayload).GetField(nameof(FunctionPointerPayload.Leaf))
                    ?? throw new InvalidOperationException("FunctionPointerPayload.Leaf was not found.");

        Assert.True(functionPointer.IsFunctionPointer);
        Assert.NotNull(body.Symbols[functionPointer]);
        Assert.NotNull(body.Symbols[typeof(FunctionPointerPayload)]);
        Assert.NotNull(body.Symbols[typeof(ReferenceLeaf)]);
        Assert.NotNull(body.Symbols[field]);
    }

    [Fact]
    public void DirectTypeFailurePoisonsParserAndCannotReturnCachedPlaceholder()
    {
        var parser = new RuntimeReflectionParser();

        Assert.Throws<InvalidOperationException>(() => parser.ParseType(typeof(BrokenAttributedStruct)));
        var typeRetry = Assert.Throws<InvalidOperationException>(() =>
            parser.ParseType(typeof(BrokenAttributedStruct)));
        var methodRetry = Assert.Throws<InvalidOperationException>(() =>
            parser.ParseMethod(GetMethod(nameof(AcceptBrokenAttributedStruct))));
        Assert.Contains("failed while collecting", typeRetry.Message);
        Assert.Contains("failed while collecting", methodRetry.Message);

        _ = new RuntimeReflectionParser().ParseMethod(GetMethod(nameof(Identity)));
    }

    [Theory]
    [InlineData(typeof(PublicPropertyShader))]
    [InlineData(typeof(PrivatePropertyShader))]
    public void AttributedModulePropertiesFailClosed(Type shaderType)
    {
        var parser = new RuntimeReflectionParser();
        ISharpShader shader = shaderType == typeof(PublicPropertyShader)
            ? new PublicPropertyShader()
            : new PrivatePropertyShader();

        var exception = Assert.Throws<NotSupportedException>(() => parser.ParseShaderModule(shader));

        Assert.Contains("property", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("use an attributed field", exception.Message);
        Assert.Throws<InvalidOperationException>(() => parser.ParseMethod(GetMethod(nameof(Identity))));
    }

    [Fact]
    public void EveryPublishedPipelineStageHasReadableModuleOutput()
    {
        var method = GetMethod(nameof(Identity));
        var raw = CompilerTestPipeline.ParseRaw(method);
        var pre = CilPreStackPass.Run(raw);
        var controlFlow = CilControlFlowPass.Run(pre);
        var values = CilStackToValuePass.Run(controlFlow);
        var facts = CilBlockControlFactsPass.Run(values);

        Assert.Contains("linear-cil raw", Format(raw));
        Assert.Contains("linear-cil pre-annotated reachable", Format(pre));
        Assert.Contains("reachable-cil-cfg", Format(controlFlow));
        Assert.Contains("flat-value-cfg", Format(values));
        Assert.Contains("control-facts-cfg", Format(facts));
        Assert.Contains(" facts={rpo=", Format(facts));
    }

    [Fact]
    public void LiteralBearingValueAndFactsStagesPrettyPrintThroughActualPipeline()
    {
        AssertLiteralStagePrinting(GetMethod(nameof(Nested)), ["0_i32", "1_i32", "5_i32", "7_i32"]);
        AssertLiteralStagePrinting(GetMethod(nameof(Return42)), ["42_i32"]);
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

    private static void AssertLiteralStagePrinting(MethodInfo method, string[] expectedLiterals)
    {
        var valueModule = CilStackToValuePass.Run(
            CilControlFlowPass.Run(
                CilPreStackPass.Run(
                    CompilerTestPipeline.ParseRaw(method))));
        var valueBody = Assert.Single(valueModule.FunctionDefinitions.Values);
        var factsBody = Assert.Single(
            CilBlockControlFactsPass.Run(valueModule).FunctionDefinitions.Values);
        var valueText = valueBody.PrettyPrint();
        var factsText = factsBody.PrettyPrint();

        foreach (var literal in expectedLiterals)
        {
            Assert.Contains(literal, valueText);
            Assert.Contains(literal, factsText);
        }
    }

    private static int Identity(int value) => value;
    private static int Increment(int value) => value + 1;
    private static int Return42() => 42;

    private static int Nested(int outer, int inner)
    {
        var sum = 0;
        for (var i = 0; i < outer; i++)
        {
            for (var j = 0; j < inner; j++)
                sum += 7;
            sum += 5;
        }

        return sum;
    }

    private static int AcceptBrokenAttributedStruct(BrokenAttributedStruct value) => 1;
    private static unsafe int FunctionPointerSignature(delegate*<FunctionPointerPayload, int> callback) => 1;

    private static int ReadCycle(LeftNode value) => value.Right.Value;

    private sealed class LeftNode
    {
        public RightNode Right = null!;
    }

    private sealed class RightNode
    {
        public LeftNode? Left = null;
        public int Value = 0;
    }

    private sealed class ReferenceLeaf
    {
    }

    private sealed class ReferenceContainer
    {
        public ReferenceLeaf? Leaf = null;
    }

    private sealed class FunctionPointerPayload
    {
        public ReferenceLeaf? Leaf = null;
    }

    private sealed class ParentMutationType;

    private class StaticOwner
    {
        private ReferenceContainer? unused = null;

        public static int Entry() => 1;
        private ReferenceContainer? ReadUnused() => unused;
    }

    private class BaseOwner
    {
        private ReferenceLeaf? Inherited = null;

        private ReferenceLeaf? ReadInherited() => Inherited;
    }

    private sealed class DerivedStaticOwner : BaseOwner
    {
        public static int Entry() => 1;
    }

    private struct SelfReturningStruct
    {
        public SelfReturningStruct Self => this;
    }

    private struct BrokenAttributedStruct
    {
        [ThrowingShader]
        public int Value => 0;
    }

    [AttributeUsage(AttributeTargets.Property)]
    private sealed class ThrowingShaderAttribute : Attribute, IShaderAttribute
    {
        public ThrowingShaderAttribute() =>
            throw new InvalidOperationException("Attribute construction must fail.");
    }

    private sealed class UnusedModuleVariableShader : ISharpShader
    {
        [Uniform]
        [Group(0)]
        [Binding(0)]
        public static readonly ReferenceContainer? Value = null;

        [Vertex]
        public static int Entry() => 1;
    }

    private sealed class PublicPropertyShader : ISharpShader
    {
        [Uniform]
        [Group(0)]
        [Binding(0)]
        private static readonly int Field = 0;

        [Uniform]
        [Group(0)]
        [Binding(1)]
        public static int Value { get; }

        [Vertex]
        public static int Entry() => Field;
    }

    private sealed class PrivatePropertyShader : ISharpShader
    {
        [Uniform]
        [Group(0)]
        [Binding(0)]
        private static readonly int Field = 0;

        [Uniform]
        [Group(0)]
        [Binding(1)]
        private static int Value { get; }

        [Vertex]
        public static int Entry() => Field;
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

    private static FieldInfo ReferenceContainerLeafField() =>
        typeof(ReferenceContainer).GetField(nameof(ReferenceContainer.Leaf))
        ?? throw new InvalidOperationException("ReferenceContainer.Leaf was not found.");

    private static MethodInfo UnusedReferenceLocalFixture()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("UnusedReferenceLocalFixture"),
            AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule("UnusedReferenceLocalFixture").DefineType(
            "UnusedReferenceLocalFixture",
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
        var method = type.DefineMethod(
            "UnusedReferenceLocal",
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(int),
            Type.EmptyTypes);
        var il = method.GetILGenerator();
        _ = il.DeclareLocal(typeof(ReferenceContainer));
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Ret);
        return (type.CreateType() ?? throw new InvalidOperationException("Fixture type creation failed."))
            .GetMethod("UnusedReferenceLocal")
            ?? throw new InvalidOperationException("Fixture method was not found.");
    }
}

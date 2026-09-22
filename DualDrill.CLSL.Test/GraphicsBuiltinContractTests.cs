using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Reflection;
using DualDrill.Mathematics;

namespace DualDrill.CLSL.Test;

[Collection(SlangProcessTestCollection.Name)]
public sealed class GraphicsBuiltinContractTests
{
    [Fact]
    public async Task InstanceIndexRetainsTypedMetadataAndTargetSemantics()
    {
        var shader = new InstanceIndexShader();
        var compiler = new CLSLCompiler(new(CLSLCompileTarget.IR));
        var module = compiler.Parse(shader);
        var vertex = module.Declarations
            .OfType<FunctionDeclaration>()
            .Single(function => function.Name == nameof(InstanceIndexShader.vs));
        var instance = vertex.Parameters.Single(parameter => parameter.Name == "instanceIndex");

        Assert.Equal(
            BuiltinBinding.instance_index,
            Assert.Single(instance.Attributes.OfType<BuiltinAttribute>()).Slot);
        Assert.Equal(ShaderType.U32, instance.Type);

        var ir = compiler.Emit(shader);
        var slang = new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(shader);
        var wgsl = new CLSLCompiler(new(CLSLCompileTarget.WGSL)).Emit(shader);

        Assert.Contains("@builtin(instance_index)", ir);
        Assert.Contains("u32 instanceIndex : SV_InstanceID", slang);
        Assert.Contains("@builtin(instance_index)", wgsl);
        Assert.Contains("@builtin(vertex_index)", wgsl);
        Assert.Contains("@location(0)", wgsl);
        Assert.Contains("@builtin(position)", wgsl);

        using var reflection = JsonDocument.Parse(await new SlangService().ReflectAsync(slang));
        var entry = reflection.RootElement
            .GetProperty("entryPoints")
            .EnumerateArray()
            .Single(candidate => candidate.GetProperty("name").GetString() == nameof(InstanceIndexShader.vs));
        Assert.Equal("vertex", entry.GetProperty("stage").GetString());
        var parameters = entry.GetProperty("parameters")
            .EnumerateArray()
            .ToDictionary(parameter => parameter.GetProperty("name").GetString()
                ?? throw new InvalidDataException("Slang reflection parameter has no name."));

        var reflectedInstance = parameters["instanceIndex"];
        Assert.Equal("SV_INSTANCEID", reflectedInstance.GetProperty("semanticName").GetString());
        AssertScalarType(reflectedInstance.GetProperty("type"), "uint32");
        Assert.Equal("SV_VERTEXID", parameters["vertexIndex"].GetProperty("semanticName").GetString());
        Assert.Equal("TEXCOORD", parameters["offset"].GetProperty("semanticName").GetString());
    }

    [Theory]
    [InlineData(typeof(WrongInstanceIndexI32Shader), "must have CLR type System.UInt32; found System.Int32")]
    [InlineData(typeof(WrongInstanceIndexVectorShader),
        "must have CLR type System.UInt32; found DualDrill.Mathematics.vec2u32")]
    [InlineData(typeof(DuplicateInstanceIndexShader), "at most one instance_index input; found 2")]
    [InlineData(typeof(UnusedHelperInstanceIndexShader),
        "instance_index requires exactly one [Vertex] stage attribute; found 0")]
    [InlineData(typeof(ReturnInstanceIndexShader), "valid only on a vertex input parameter")]
    [InlineData(typeof(FragmentInstanceIndexShader),
        "instance_index requires exactly one [Vertex] stage attribute; found [Fragment]")]
    [InlineData(typeof(ComputeInstanceIndexShader),
        "instance_index requires exactly one [Vertex] stage attribute; found [Compute]")]
    [InlineData(typeof(MixedStageInstanceIndexShader),
        "instance_index requires exactly one [Vertex] stage attribute; found [Fragment], [Vertex]")]
    [InlineData(typeof(ConflictingLocationInstanceIndexShader),
        "exactly one interface attribute is allowed; found [Builtin], [Location]")]
    public void InvalidClrInstanceIndexMetadataIsRejected(Type shaderType, string expected)
    {
        var shader = (ISharpShader)(Activator.CreateInstance(shaderType)
            ?? throw new InvalidOperationException($"Could not create {shaderType}."));

        var exception = Assert.Throws<NotSupportedException>(() =>
            new CLSLCompiler(new(CLSLCompileTarget.IR)).Parse(shader));

        Assert.StartsWith("Shader module metadata validation rejected ", exception.Message);
        Assert.Contains(expected, exception.Message);
    }

    [Fact]
    public void RawIdenticalInstanceIndexAttributesAreCountedBeforeSetConversion()
    {
        var method = CreateRawInstanceIndexMethod(vertexCount: 1, instanceCount: 2);
        Assert.Equal(
            2,
            Assert.Single(method.GetParameters())
                .GetCustomAttributes<BuiltinAttribute>(inherit: false)
                .Count(attribute => attribute.Slot is BuiltinBinding.instance_index));

        var exception = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseMethod(method));

        Assert.Equal(
            $"Shader module metadata validation rejected method " +
            $"'{method.DeclaringType?.FullName}.{method.Name}': " +
            "a vertex entry accepts at most one instance_index input; found 2.",
            exception.Message);
    }

    [Fact]
    public void RawIdenticalVertexAttributesAreCountedBeforeSetConversion()
    {
        var method = CreateRawInstanceIndexMethod(vertexCount: 2, instanceCount: 1);
        Assert.Equal(2, method.GetCustomAttributes<VertexAttribute>(inherit: false).Count());

        var exception = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseMethod(method));

        Assert.Equal(
            $"Shader module metadata validation rejected method " +
            $"'{method.DeclaringType?.FullName}.{method.Name}': " +
            "instance_index requires exactly one [Vertex] stage attribute; found [Vertex], [Vertex].",
            exception.Message);
    }

    [Fact]
    public void DirectIrValidationAppliesInstanceRulesBeforeGenericInterfaceValidation()
    {
        var duplicate = Function(
            "Duplicate",
            [new VertexAttribute()],
            new ParameterDeclaration(
                "first",
                ShaderType.U32,
                [
                    new BuiltinAttribute(BuiltinBinding.instance_index),
                    new LocationAttribute(0)
                ]),
            new ParameterDeclaration(
                "second",
                ShaderType.U32,
                [new BuiltinAttribute(BuiltinBinding.instance_index)]));
        AssertDirectIrRejected(
            duplicate,
            "a vertex entry accepts at most one instance_index input; found 2");

        var wrongType = Function(
            "WrongType",
            [new VertexAttribute()],
            new ParameterDeclaration(
                "instance",
                ShaderType.I32,
                [new BuiltinAttribute(BuiltinBinding.instance_index)]));
        AssertDirectIrRejected(wrongType, "instance_index must have type u32; found i32");

        var helper = Function(
            "Helper",
            [],
            new ParameterDeclaration(
                "instance",
                ShaderType.U32,
                [new BuiltinAttribute(BuiltinBinding.instance_index)]));
        AssertDirectIrRejected(
            helper,
            "instance_index requires exactly one [Vertex] stage attribute; found 0");

        var mixedStage = Function(
            "MixedStage",
            [new VertexAttribute(), new FragmentAttribute()],
            new ParameterDeclaration(
                "instance",
                ShaderType.U32,
                [new BuiltinAttribute(BuiltinBinding.instance_index)]));
        AssertDirectIrRejected(
            mixedStage,
            "instance_index requires exactly one [Vertex] stage attribute; found [Fragment], [Vertex]");

        var returned = new FunctionDeclaration(
            "Returned",
            [],
            new FunctionReturn(
                ShaderType.U32,
                [new BuiltinAttribute(BuiltinBinding.instance_index)]),
            [new VertexAttribute()]);
        AssertDirectIrRejected(returned, "valid only on a vertex input parameter");

        var conflicting = Function(
            "Conflicting",
            [new VertexAttribute()],
            new ParameterDeclaration(
                "instance",
                ShaderType.U32,
                [
                    new BuiltinAttribute(BuiltinBinding.instance_index),
                    new LocationAttribute(0)
                ]));
        AssertDirectIrRejected(
            conflicting,
            "exactly one interface attribute is allowed; found [Builtin], [Location]");
    }

    private static void AssertScalarType(JsonElement type, string scalarType)
    {
        Assert.Equal("scalar", type.GetProperty("kind").GetString());
        Assert.Equal(scalarType, type.GetProperty("scalarType").GetString());
    }

    private static FunctionDeclaration Function(
        string name,
        ImmutableHashSet<IShaderAttribute> attributes,
        params ParameterDeclaration[] parameters) =>
        new(
            name,
            [.. parameters],
            new FunctionReturn(UnitType.Instance, []),
            attributes);

    private static void AssertDirectIrRejected(FunctionDeclaration function, string expected)
    {
        var module = new ShaderModuleDeclaration<RegionFunctionBody>(
            [function],
            ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty);

        var exception = Assert.Throws<NotSupportedException>(() =>
            new ShaderModuleReflection().GetUniformBindings(module));

        Assert.StartsWith("Shader module metadata validation rejected ", exception.Message);
        Assert.Contains(expected, exception.Message);
    }

    private static MethodInfo CreateRawInstanceIndexMethod(int vertexCount, int instanceCount)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"InstanceIndexRawMetadata_{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("InstanceIndexRawMetadata");
        var type = module.DefineType(
            "RawInstanceIndexShader",
            TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed);
        var method = type.DefineMethod(
            "Vertex",
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(void),
            [typeof(uint)]);
        for (var index = 0; index < vertexCount; index++)
            method.SetCustomAttribute(ParameterlessAttribute<VertexAttribute>());
        var parameter = method.DefineParameter(1, ParameterAttributes.None, "instance");
        for (var index = 0; index < instanceCount; index++)
            parameter.SetCustomAttribute(BuiltinAttribute(BuiltinBinding.instance_index));
        method.GetILGenerator().Emit(OpCodes.Ret);

        var shaderType = type.CreateType()
            ?? throw new InvalidOperationException("Dynamic instance-index shader type was not created.");
        return shaderType.GetMethod("Vertex")
            ?? throw new InvalidOperationException("Dynamic instance-index entry was not created.");
    }

    private static CustomAttributeBuilder ParameterlessAttribute<TAttribute>()
        where TAttribute : Attribute =>
        new(
            typeof(TAttribute).GetConstructor(Type.EmptyTypes)
            ?? throw new InvalidOperationException($"{typeof(TAttribute)} has no parameterless constructor."),
            []);

    private static CustomAttributeBuilder BuiltinAttribute(BuiltinBinding value) =>
        new(
            typeof(BuiltinAttribute).GetConstructor([typeof(BuiltinBinding)])
            ?? throw new InvalidOperationException("BuiltinAttribute constructor was not found."),
            [value]);

    private sealed class InstanceIndexShader : ISharpShader
    {
        [Vertex]
        [return: Builtin(BuiltinBinding.position)]
        public static vec4f32 vs(
            [Builtin(BuiltinBinding.vertex_index)] uint vertexIndex,
            [Builtin(BuiltinBinding.instance_index)] uint instanceIndex,
            [Location(0)] vec2f32 offset)
        {
            if (instanceIndex == 0u)
                return DMath.vec4(-0.5f, -0.5f, 0.0f, 1.0f);

            return DMath.vec4(0.5f, 0.5f, 0.0f, 1.0f);
        }

        [Fragment]
        [return: Location(0)]
        public static vec4f32 fs() => DMath.vec4(1.0f, 0.5f, 0.25f, 1.0f);
    }

    private sealed class WrongInstanceIndexI32Shader : ISharpShader
    {
        [Vertex]
        [return: Builtin(BuiltinBinding.position)]
        public static vec4f32 vs([Builtin(BuiltinBinding.instance_index)] int instance) =>
            DMath.vec4(0.0f);
    }

    private sealed class WrongInstanceIndexVectorShader : ISharpShader
    {
        [Vertex]
        [return: Builtin(BuiltinBinding.position)]
        public static vec4f32 vs([Builtin(BuiltinBinding.instance_index)] vec2u32 instance) =>
            DMath.vec4(0.0f);
    }

    private sealed class DuplicateInstanceIndexShader : ISharpShader
    {
        [Vertex]
        [return: Builtin(BuiltinBinding.position)]
        public static vec4f32 vs(
            [Builtin(BuiltinBinding.instance_index)] uint first,
            [Builtin(BuiltinBinding.instance_index)] uint second) =>
            DMath.vec4(0.0f);
    }

    private sealed class UnusedHelperInstanceIndexShader : ISharpShader
    {
        [Vertex]
        [return: Builtin(BuiltinBinding.position)]
        public static vec4f32 vs() => DMath.vec4(0.0f);

        private static uint Helper([Builtin(BuiltinBinding.instance_index)] uint instance) => instance;
    }

    private sealed class ReturnInstanceIndexShader : ISharpShader
    {
        [Vertex]
        [return: Builtin(BuiltinBinding.instance_index)]
        public static uint vs() => 0u;
    }

    private sealed class FragmentInstanceIndexShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static vec4f32 fs([Builtin(BuiltinBinding.instance_index)] uint instance) =>
            DMath.vec4(0.0f);
    }

    private sealed class ComputeInstanceIndexShader : ISharpShader
    {
        [Compute, WorkgroupSize(1, 1, 1)]
        public static void Run([Builtin(BuiltinBinding.instance_index)] uint instance)
        {
        }
    }

    private sealed class MixedStageInstanceIndexShader : ISharpShader
    {
        [Vertex, Fragment]
        [return: Builtin(BuiltinBinding.position)]
        public static vec4f32 Run([Builtin(BuiltinBinding.instance_index)] uint instance) =>
            DMath.vec4(0.0f);
    }

    private sealed class ConflictingLocationInstanceIndexShader : ISharpShader
    {
        [Vertex]
        [return: Builtin(BuiltinBinding.position)]
        public static vec4f32 vs(
            [Builtin(BuiltinBinding.instance_index), Location(0)] uint instance) =>
            DMath.vec4(0.0f);
    }
}

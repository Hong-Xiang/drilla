using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using DualDrill.Mathematics;

namespace DualDrill.CLSL.Test;

[Collection(SlangProcessTestCollection.Name)]
public sealed class ComputeEntryContractTests
{
    [Fact]
    public void WorkgroupSizeAttributeIsMethodOnlyNonInheritedAndNonRepeatable()
    {
        var usage = Assert.Single(
            typeof(WorkgroupSizeAttribute).GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
                .Cast<AttributeUsageAttribute>());

        Assert.Equal(AttributeTargets.Method, usage.ValidOn);
        Assert.False(usage.Inherited);
        Assert.False(usage.AllowMultiple);
    }

    [Fact]
    public void PositiveInt32DimensionsDoNotAcquireCompilerDeviceLimits()
    {
        var module = new CLSLCompiler(new(CLSLCompileTarget.IR)).Parse(new LargeWorkgroupShader());
        var function = Assert.Single(module.Declarations.OfType<FunctionDeclaration>());
        var size = Assert.Single(function.Attributes.OfType<WorkgroupSizeAttribute>());

        Assert.Equal((int.MaxValue, 1, 1), (size.X, size.Y, size.Z));
    }

    [Fact]
    public void ZeroInputComputeEntryEmitsSlangAndWgsl()
    {
        var shader = new ZeroInputComputeShader();

        var slang = Emit(CLSLCompileTarget.SLang, shader);
        var wgsl = Emit(CLSLCompileTarget.WGSL, shader);

        Assert.Contains("[shader(\"compute\")]", slang);
        Assert.Contains("[numthreads(64, 1, 1)]", slang);
        Assert.Contains("void Run()", slang);
        Assert.Contains("@compute", wgsl);
        Assert.Contains("@workgroup_size(64, 1, 1)", wgsl);
    }

    [Fact]
    public void GlobalInvocationIdComputeEntryRetainsTypedMetadataAndEmitsBothTargets()
    {
        var shader = new GlobalInvocationIdComputeShader();
        var compiler = new CLSLCompiler(new(CLSLCompileTarget.IR));
        var raw = compiler.Parse(shader);
        var function = Assert.Single(raw.Declarations.OfType<FunctionDeclaration>());
        var parameter = Assert.Single(function.Parameters);

        Assert.Single(function.Attributes.OfType<ComputeAttribute>());
        var size = Assert.Single(function.Attributes.OfType<WorkgroupSizeAttribute>());
        Assert.Equal((64, 1, 1), (size.X, size.Y, size.Z));
        Assert.Equal(
            BuiltinBinding.global_invocation_id,
            Assert.Single(parameter.Attributes.OfType<BuiltinAttribute>()).Slot);
        Assert.Equal(VecType<N3, UIntType<N32>>.Instance, parameter.Type);

        var ir = compiler.Emit(shader);
        var slang = Emit(CLSLCompileTarget.SLang, shader);
        var wgsl = Emit(CLSLCompileTarget.WGSL, shader);

        Assert.Contains("@compute", ir);
        Assert.Contains("@workgroup_size(64, 1, 1)", ir);
        Assert.Contains("@builtin(global_invocation_id)", ir);
        Assert.Contains("[shader(\"compute\")]", slang);
        Assert.Contains("[numthreads(64, 1, 1)]", slang);
        Assert.Contains("vec3<u32> id : SV_DispatchThreadID", slang);
        Assert.Contains("@compute", wgsl);
        Assert.Contains("@workgroup_size(64, 1, 1)", wgsl);
        Assert.Contains("@builtin(global_invocation_id)", wgsl);
        Assert.Contains("vec3<u32>", wgsl);
    }

    [Fact]
    public async Task SlangReflectionRetainsComputeStageAndWorkgroupSize()
    {
        var slang = Emit(CLSLCompileTarget.SLang, new GlobalInvocationIdComputeShader());
        using var reflection = JsonDocument.Parse(await new SlangService().ReflectAsync(slang));
        var entryPoint = Assert.Single(reflection.RootElement.GetProperty("entryPoints").EnumerateArray());

        Assert.Equal("compute", entryPoint.GetProperty("stage").GetString());
        Assert.Equal(
            [64, 1, 1],
            entryPoint.GetProperty("threadGroupSize").EnumerateArray().Select(value => value.GetInt32()));
    }

    [Theory]
    [InlineData(typeof(MissingWorkgroupShader), "exactly one [WorkgroupSize] attribute; found 0")]
    [InlineData(typeof(ZeroWorkgroupXShader), "found (0, 1, 1)")]
    [InlineData(typeof(ZeroWorkgroupYShader), "found (1, 0, 1)")]
    [InlineData(typeof(NegativeWorkgroupZShader), "found (1, 1, -1)")]
    [InlineData(typeof(InstanceComputeShader), "must be static")]
    [InlineData(typeof(NonVoidComputeShader), "must return void")]
    [InlineData(typeof(MultiStageComputeShader), "exactly one shader stage attribute; found 2")]
    [InlineData(typeof(WorkgroupOnVertexShader), "[WorkgroupSize] is valid only on a [Compute] entry point")]
    [InlineData(typeof(WrongGlobalInvocationIdTypeShader), "must have type vec3<u32>")]
    [InlineData(typeof(GlobalInvocationIdOnVertexShader), "valid only on a [Compute] entry input")]
    [InlineData(typeof(LocationComputeInputShader), "only supported compute input")]
    [InlineData(typeof(WrongBuiltinComputeInputShader), "only supported compute input")]
    [InlineData(typeof(DuplicateGlobalInvocationIdShader), "at most one global_invocation_id input; found 2")]
    [InlineData(typeof(ComputeReturnSemanticShader), "return must not have interface attributes")]
    [InlineData(typeof(UnusedWorkgroupHelperShader), "[WorkgroupSize] is valid only on a [Compute] entry point")]
    [InlineData(typeof(UnusedGlobalInvocationIdHelperShader),
        "[Builtin(global_invocation_id)] is valid only on a [Compute] entry input")]
    public void InvalidReflectedComputeMetadataIsRejected(Type shaderType, string expected)
    {
        var shader = (ISharpShader)(Activator.CreateInstance(shaderType)
            ?? throw new InvalidOperationException($"Could not create {shaderType}."));

        var exception = Assert.Throws<NotSupportedException>(() =>
            new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(shader));

        Assert.StartsWith("Shader module metadata validation rejected ", exception.Message);
        Assert.Contains(expected, exception.Message);
    }

    [Fact]
    public void PublicRawCompileRejectsRepeatedWorkgroupMetadataBeforeCilPasses()
    {
        var function = new FunctionDeclaration(
            "Run",
            [],
            new FunctionReturn(UnitType.Instance, []),
            [
                new ComputeAttribute(),
                new WorkgroupSizeAttribute(64, 1, 1),
                new WorkgroupSizeAttribute(32, 1, 1)
            ]);
        var module = new ShaderModuleDeclaration<RawCilFunctionBody>(
            [function],
            ImmutableDictionary<FunctionDeclaration, RawCilFunctionBody>.Empty);

        var exception = Assert.Throws<NotSupportedException>(() =>
            new CLSLCompiler(new(CLSLCompileTarget.IR)).Compile(module));

        Assert.Equal(
            "Shader module metadata validation rejected function 'Run': " +
            "a compute entry requires exactly one [WorkgroupSize] attribute; found 2.",
            exception.Message);
    }

    [Fact]
    public void DirectIrRejectsDuplicateGlobalInvocationIdInputs()
    {
        var first = new ParameterDeclaration(
            "first",
            VecType<N3, UIntType<N32>>.Instance,
            [new BuiltinAttribute(BuiltinBinding.global_invocation_id)]);
        var second = new ParameterDeclaration(
            "second",
            VecType<N3, UIntType<N32>>.Instance,
            [new BuiltinAttribute(BuiltinBinding.global_invocation_id)]);
        var function = new FunctionDeclaration(
            "Run",
            [first, second],
            new FunctionReturn(UnitType.Instance, []),
            [new ComputeAttribute(), new WorkgroupSizeAttribute(64, 1, 1)]);
        var module = new ShaderModuleDeclaration<RawCilFunctionBody>(
            [function],
            ImmutableDictionary<FunctionDeclaration, RawCilFunctionBody>.Empty);

        var exception = Assert.Throws<NotSupportedException>(() =>
            new CLSLCompiler(new(CLSLCompileTarget.IR)).Compile(module));

        Assert.Equal(
            "Shader module metadata validation rejected function 'Run': " +
            "a compute entry accepts at most one global_invocation_id input; found 2.",
            exception.Message);
    }

    [Fact]
    public void ParseMethodRejectsInstanceComputeEntryAtReflectionBoundary()
    {
        var method = typeof(InstanceComputeShader).GetMethod(nameof(InstanceComputeShader.Run))
            ?? throw new InvalidOperationException("Compute entry method was not found.");

        var exception = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseMethod(method));

        Assert.Contains(nameof(InstanceComputeShader), exception.Message);
        Assert.Contains("a compute entry point must be static", exception.Message);
    }

    [Fact]
    public void InheritedInstanceComputeEntryIsRejectedAtReflectionBoundary()
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseShaderModule(new InheritedInstanceComputeShader()));

        Assert.Contains(nameof(InheritedComputeBase), exception.Message);
        Assert.Contains("a compute entry point must be static", exception.Message);
    }

    [Fact]
    public void DirectIrComputeEntryRemainsAFreeFunction()
    {
        var function = new FunctionDeclaration(
            "Run",
            [],
            new FunctionReturn(UnitType.Instance, []),
            [new ComputeAttribute(), new WorkgroupSizeAttribute(64, 1, 1)]);
        var module = new ShaderModuleDeclaration<RawCilFunctionBody>(
            [function],
            ImmutableDictionary<FunctionDeclaration, RawCilFunctionBody>.Empty);

        var compiled = new CLSLCompiler(new(CLSLCompileTarget.IR)).Compile(module);

        Assert.Same(function, Assert.Single(compiled.Declarations.OfType<FunctionDeclaration>()));
    }

    [Fact]
    public void RawDuplicateComputeAttributesAreRejectedByModuleParsing()
    {
        var (shaderType, method) = CreateRawComputeShader(computeCount: 2, workgroupSizeCount: 1);
        Assert.Equal(2, method.GetCustomAttributes<ComputeAttribute>(inherit: false).Count());
        var shader = (ISharpShader)(Activator.CreateInstance(shaderType)
            ?? throw new InvalidOperationException($"Could not create {shaderType}."));

        var exception = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseShaderModule(shader));

        Assert.Equal(
            $"Shader module metadata validation rejected method '{shaderType.FullName}.Run': " +
            "a compute entry requires exactly one shader stage attribute; found 2.",
            exception.Message);
    }

    [Fact]
    public void RawDuplicateWorkgroupSizeAttributesAreRejectedByDirectMethodParsing()
    {
        var (shaderType, method) = CreateRawComputeShader(computeCount: 1, workgroupSizeCount: 2);
        Assert.Equal(2, method.GetCustomAttributes<WorkgroupSizeAttribute>(inherit: false).Count());

        var exception = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseMethod(method));

        Assert.Equal(
            $"Shader module metadata validation rejected method '{shaderType.FullName}.Run': " +
            "a compute entry requires exactly one [WorkgroupSize] attribute; found 2.",
            exception.Message);
    }

    [Fact]
    public void RawDuplicateGlobalInvocationIdAttributesAreRejectedInComputeModule()
    {
        var (shaderType, method) = CreateRawComputeShader(
            computeCount: 1,
            workgroupSizeCount: 1,
            duplicateParameterBuiltin: true);
        var parameter = Assert.Single(method.GetParameters());
        Assert.Equal(2, parameter.GetCustomAttributes<BuiltinAttribute>(inherit: false).Count());
        var shader = (ISharpShader)(Activator.CreateInstance(shaderType)
            ?? throw new InvalidOperationException($"Could not create {shaderType}."));

        var exception = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseShaderModule(shader));

        Assert.Equal(
            $"Shader module metadata validation rejected parameter '{shaderType.FullName}.Run.id': " +
            "exactly one interface attribute is allowed; found [Builtin], [Builtin].",
            exception.Message);
    }

    [Fact]
    public void RawDuplicateReturnLocationsAreRejectedInDirectComputeMethodParsing()
    {
        var (shaderType, method) = CreateRawComputeShader(
            computeCount: 1,
            workgroupSizeCount: 1,
            duplicateReturnLocation: true);
        Assert.Equal(2, method.ReturnParameter.GetCustomAttributes<LocationAttribute>(inherit: false).Count());

        var exception = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseMethod(method));

        Assert.Equal(
            $"Shader module metadata validation rejected return of method '{shaderType.FullName}.Run': " +
            "exactly one interface attribute is allowed; found [Location], [Location].",
            exception.Message);
    }

    private static string Emit(CLSLCompileTarget target, ISharpShader shader) =>
        new CLSLCompiler(new(target)).Emit(shader);

    private static (Type ShaderType, MethodInfo Method) CreateRawComputeShader(
        int computeCount,
        int workgroupSizeCount,
        bool duplicateParameterBuiltin = false,
        bool duplicateReturnLocation = false)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"ComputeEntryRawMetadata_{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("ComputeEntryRawMetadata");
        var type = module.DefineType(
            "RawComputeShader",
            TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed);
        type.AddInterfaceImplementation(typeof(ISharpShader));
        type.DefineDefaultConstructor(MethodAttributes.Public);
        var method = type.DefineMethod(
            "Run",
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(void),
            duplicateParameterBuiltin ? [typeof(vec3u32)] : Type.EmptyTypes);
        var computeConstructor = typeof(ComputeAttribute).GetConstructor(Type.EmptyTypes)
            ?? throw new InvalidOperationException("ComputeAttribute constructor was not found.");
        var workgroupSizeConstructor = typeof(WorkgroupSizeAttribute).GetConstructor(
            [typeof(int), typeof(int), typeof(int)])
            ?? throw new InvalidOperationException("WorkgroupSizeAttribute constructor was not found.");
        var builtinConstructor = typeof(BuiltinAttribute).GetConstructor([typeof(BuiltinBinding)])
            ?? throw new InvalidOperationException("BuiltinAttribute constructor was not found.");
        var locationConstructor = typeof(LocationAttribute).GetConstructor([typeof(int)])
            ?? throw new InvalidOperationException("LocationAttribute constructor was not found.");

        for (var index = 0; index < computeCount; index++)
            method.SetCustomAttribute(new CustomAttributeBuilder(computeConstructor, []));
        for (var index = 0; index < workgroupSizeCount; index++)
            method.SetCustomAttribute(new CustomAttributeBuilder(workgroupSizeConstructor, [64, 1, 1]));
        if (duplicateParameterBuiltin)
        {
            var parameter = method.DefineParameter(1, ParameterAttributes.None, "id");
            parameter.SetCustomAttribute(
                new CustomAttributeBuilder(builtinConstructor, [BuiltinBinding.global_invocation_id]));
            parameter.SetCustomAttribute(
                new CustomAttributeBuilder(builtinConstructor, [BuiltinBinding.global_invocation_id]));
        }
        if (duplicateReturnLocation)
        {
            var result = method.DefineParameter(0, ParameterAttributes.Retval, null);
            result.SetCustomAttribute(new CustomAttributeBuilder(locationConstructor, [0]));
            result.SetCustomAttribute(new CustomAttributeBuilder(locationConstructor, [0]));
        }
        method.GetILGenerator().Emit(OpCodes.Ret);

        var shaderType = type.CreateType()
            ?? throw new InvalidOperationException("Dynamic compute shader type was not created.");
        return (
            shaderType,
            shaderType.GetMethod("Run")
            ?? throw new InvalidOperationException("Dynamic compute entry was not created."));
    }

    private sealed class ZeroInputComputeShader : ISharpShader
    {
        [Compute, WorkgroupSize(64, 1, 1)]
        public static void Run()
        {
        }
    }

    private sealed class GlobalInvocationIdComputeShader : ISharpShader
    {
        [Compute, WorkgroupSize(64, 1, 1)]
        public static void Run(
            [Builtin(BuiltinBinding.global_invocation_id)]
            vec3u32 id)
        {
        }
    }

    private sealed class LargeWorkgroupShader : ISharpShader
    {
        [Compute, WorkgroupSize(int.MaxValue, 1, 1)]
        public static void Run()
        {
        }
    }

    private sealed class MissingWorkgroupShader : ISharpShader
    {
        [Compute]
        public static void Run()
        {
        }
    }

    private sealed class ZeroWorkgroupXShader : ISharpShader
    {
        [Compute, WorkgroupSize(0, 1, 1)]
        public static void Run()
        {
        }
    }

    private sealed class ZeroWorkgroupYShader : ISharpShader
    {
        [Compute, WorkgroupSize(1, 0, 1)]
        public static void Run()
        {
        }
    }

    private sealed class NegativeWorkgroupZShader : ISharpShader
    {
        [Compute, WorkgroupSize(1, 1, -1)]
        public static void Run()
        {
        }
    }

    private sealed class InstanceComputeShader : ISharpShader
    {
        [Compute, WorkgroupSize(1, 1, 1)]
        public void Run()
        {
        }
    }

    private class InheritedComputeBase
    {
        [Compute, WorkgroupSize(1, 1, 1)]
        public void Run()
        {
        }
    }

    private sealed class InheritedInstanceComputeShader : InheritedComputeBase, ISharpShader;

    private sealed class NonVoidComputeShader : ISharpShader
    {
        [Compute, WorkgroupSize(1, 1, 1)]
        public static int Run() => 1;
    }

    private sealed class MultiStageComputeShader : ISharpShader
    {
        [Compute, Vertex, WorkgroupSize(1, 1, 1)]
        public static void Run()
        {
        }
    }

    private sealed class WrongGlobalInvocationIdTypeShader : ISharpShader
    {
        [Compute, WorkgroupSize(1, 1, 1)]
        public static void Run(
            [Builtin(BuiltinBinding.global_invocation_id)]
            vec3i32 id)
        {
        }
    }

    private sealed class WorkgroupOnVertexShader : ISharpShader
    {
        [Vertex, WorkgroupSize(1, 1, 1)]
        public static void Run()
        {
        }
    }

    private sealed class GlobalInvocationIdOnVertexShader : ISharpShader
    {
        [Vertex]
        public static void Run(
            [Builtin(BuiltinBinding.global_invocation_id)]
            vec3u32 id)
        {
        }
    }

    private sealed class LocationComputeInputShader : ISharpShader
    {
        [Compute, WorkgroupSize(1, 1, 1)]
        public static void Run([Location(0)] vec3u32 id)
        {
        }
    }

    private sealed class WrongBuiltinComputeInputShader : ISharpShader
    {
        [Compute, WorkgroupSize(1, 1, 1)]
        public static void Run([Builtin(BuiltinBinding.vertex_index)] uint id)
        {
        }
    }

    private sealed class DuplicateGlobalInvocationIdShader : ISharpShader
    {
        [Compute, WorkgroupSize(1, 1, 1)]
        public static void Run(
            [Builtin(BuiltinBinding.global_invocation_id)]
            vec3u32 first,
            [Builtin(BuiltinBinding.global_invocation_id)]
            vec3u32 second)
        {
        }
    }

    private sealed class ComputeReturnSemanticShader : ISharpShader
    {
        [Compute, WorkgroupSize(1, 1, 1)]
        [return: Location(0)]
        public static void Run()
        {
        }
    }

    private sealed class UnusedWorkgroupHelperShader : ISharpShader
    {
        [Vertex]
        public static int Entry() => 1;

        [WorkgroupSize(1, 1, 1)]
        private static void Helper()
        {
        }
    }

    private sealed class UnusedGlobalInvocationIdHelperShader : ISharpShader
    {
        [Vertex]
        public static int Entry() => 1;

        private static void Helper(
            [Builtin(BuiltinBinding.global_invocation_id)]
            vec3u32 id)
        {
        }
    }
}

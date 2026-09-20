using System.Collections.Immutable;
using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Test.ShaderModule;
using DualDrill.Shaders;

namespace DualDrill.CLSL.Test;

public sealed class ShaderResourceBindingContractTests
{
    [Fact]
    public void CompleteDistinctBindingPairsArePreserved()
    {
        var module = Parse(new ValidBindingsShader());
        var resources = module.Declarations.OfType<VariableDeclaration>()
            .OrderBy(declaration => declaration.Name)
            .ToArray();

        Assert.Collection(
            resources,
            first =>
            {
                Assert.Equal("First", first.Name);
                Assert.Equal(1, Assert.Single(first.Attributes.OfType<GroupAttribute>()).Binding);
                Assert.Equal(2, Assert.Single(first.Attributes.OfType<BindingAttribute>()).Binding);
            },
            second =>
            {
                Assert.Equal("SameBindingDifferentGroup", second.Name);
                Assert.Equal(2, Assert.Single(second.Attributes.OfType<GroupAttribute>()).Binding);
                Assert.Equal(2, Assert.Single(second.Attributes.OfType<BindingAttribute>()).Binding);
            });
    }

    [Fact]
    public void ExistingShaderExamplesRemainValid()
    {
        ISharpShader[] shaders =
        [
            new MinimumHelloTriangleShaderModule(),
            new SimpleStructUniformShaderModule(),
            new MandelbrotDistanceShaderModule(),
            new RaymarchingPrimitiveShader()
        ];

        Assert.All(shaders, shader => Assert.NotNull(Parse(shader)));
    }

    [Theory]
    [InlineData(typeof(MissingGroupShader), "found 1, 0, and 1")]
    [InlineData(typeof(MissingBindingShader), "found 1, 1, and 0")]
    [InlineData(typeof(MissingBothCoordinatesShader), "found 1, 0, and 0")]
    [InlineData(typeof(NegativeGroupShader), "group must be nonnegative; found -1")]
    [InlineData(typeof(NegativeBindingShader), "binding must be nonnegative; found -1")]
    public void InvalidResourceCoordinatesAreRejected(Type shaderType, string expected)
    {
        var exception = Assert.Throws<NotSupportedException>(() => Parse(Create(shaderType)));

        Assert.StartsWith("Shader module metadata validation rejected field '", exception.Message);
        Assert.Contains(expected, exception.Message);
    }

    [Fact]
    public void DuplicateBindingPairIsRejectedModuleWide()
    {
        var exception = Assert.Throws<NotSupportedException>(() => Parse(new DuplicateBindingShader()));

        Assert.Equal(
            "Shader module metadata validation rejected module: resource binding (1, 2) is duplicated by " +
            "'First' and 'Second'.",
            exception.Message);
    }

    [Fact]
    public void DirectIrRejectsRepeatedCoordinateAttributesThatCSharpAttributeUsagePrevents()
    {
        var resource = new VariableDeclaration(
            UniformAddressSpace.Instance,
            "Data",
            ShaderType.F32,
            [
                new UniformAttribute(),
                new GroupAttribute(1),
                new GroupAttribute(3),
                new BindingAttribute(2)
            ]);
        var module = new ShaderModuleDeclaration<RegionFunctionBody>(
            [resource],
            ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty);

        var exception = Assert.Throws<NotSupportedException>(() =>
            new SlangTargetLowering().Lower(module));

        Assert.Equal(
            "Shader module metadata validation rejected resource 'Data': a resource requires exactly one " +
            "address-space, [Group], and [Binding] attribute (found 1, 2, and 1).",
            exception.Message);
    }

    [Fact]
    public void UniformVisibilityHintsArePreserved()
    {
        var resource = Assert.Single(
            Parse(new VisibilityHintsShader()).Declarations.OfType<VariableDeclaration>());

        Assert.Single(resource.Attributes.OfType<VertexAttribute>());
        Assert.Single(resource.Attributes.OfType<FragmentAttribute>());
        Assert.Single(resource.Attributes.OfType<ComputeAttribute>());
    }

    [Fact]
    public void DirectIrPreservesKnownUniformVisibilityHints()
    {
        var resource = new VariableDeclaration(
            UniformAddressSpace.Instance,
            "Data",
            ShaderType.F32,
            [
                new UniformAttribute(),
                new GroupAttribute(1),
                new BindingAttribute(2),
                new VertexAttribute(),
                new FragmentAttribute(),
                new ComputeAttribute()
            ]);
        var module = new ShaderModuleDeclaration<RegionFunctionBody>(
            [resource],
            ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty);

        var lowered = new SlangTargetLowering().Lower(module);

        Assert.Same(resource, Assert.Single(lowered.Declarations.OfType<VariableDeclaration>()));
    }

    [Fact]
    public void UnknownUniformVisibilityHintIsRejected()
    {
        var resource = new VariableDeclaration(
            UniformAddressSpace.Instance,
            "Data",
            ShaderType.F32,
            [
                new UniformAttribute(),
                new GroupAttribute(1),
                new BindingAttribute(2),
                new ShaderMethodAttribute()
            ]);
        var module = new ShaderModuleDeclaration<RegionFunctionBody>(
            [resource],
            ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty);

        var exception = Assert.Throws<NotSupportedException>(() =>
            new SlangTargetLowering().Lower(module));

        Assert.Equal(
            "Shader module metadata validation rejected resource 'Data': " +
            "resource attribute(s) [ShaderMethod] are not supported.",
            exception.Message);
    }

    [Fact]
    public void ShaderModuleTypeMetadataIsRejectedBeforeOpaqueTypeMapping()
    {
        var exception = Assert.Throws<NotSupportedException>(() => Parse(new AnnotatedShaderType()));

        Assert.Contains("shader module type", exception.Message);
        Assert.Contains(nameof(AnnotatedShaderType), exception.Message);
        Assert.Contains("[Group]", exception.Message);
    }

    [Fact]
    public void UsedStructureTypeMetadataIsRejected()
    {
        var exception = Assert.Throws<NotSupportedException>(() => Parse(new AnnotatedPayloadShader()));

        Assert.Equal(
            "Shader module metadata validation rejected structure 'AnnotatedPayload': " +
            "attribute(s) [Align] are not supported.",
            exception.Message);
    }

    [Fact]
    public void ReferencedClassTypeMetadataIsRejectedBeforeOpaqueTypeMapping()
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            Parse(new AnnotatedReferencePayloadShader()));

        Assert.Equal(
            $"Shader module metadata validation rejected reference type '{typeof(AnnotatedReferencePayload).FullName}': " +
            "attribute(s) [Align] are not supported.",
            exception.Message);
    }

    [Fact]
    public void DirectIrStructureTypeMetadataIsRejected()
    {
        var structure = new StructureDeclaration
        {
            Name = "Payload",
            Attributes = [new AlignAttribute(16)],
            Members = []
        };
        var module = new ShaderModuleDeclaration<RegionFunctionBody>(
            [structure],
            ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty);

        var exception = Assert.Throws<NotSupportedException>(() =>
            new SlangTargetLowering().Lower(module));

        Assert.Equal(
            "Shader module metadata validation rejected structure 'Payload': " +
            "attribute(s) [Align] are not supported.",
            exception.Message);
    }

    [Fact]
    public void DirectIrOrdinaryVariableMetadataIsRejected()
    {
        var variable = new VariableDeclaration(
            GenericAddressSpace.Instance,
            "Data",
            ShaderType.F32,
            [new AlignAttribute(16)]);
        var module = new ShaderModuleDeclaration<RegionFunctionBody>(
            [variable],
            ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty);

        var exception = Assert.Throws<NotSupportedException>(() =>
            new SlangTargetLowering().Lower(module));

        Assert.Equal(
            "Shader module metadata validation rejected module variable 'Data': " +
            "attribute(s) [Align] are not valid on an ordinary module variable.",
            exception.Message);
    }

    [Fact]
    public void UnannotatedDirectIrOrdinaryVariableIsPreserved()
    {
        var variable = new VariableDeclaration(
            GenericAddressSpace.Instance,
            "Data",
            ShaderType.F32,
            []);
        var module = new ShaderModuleDeclaration<RegionFunctionBody>(
            [variable],
            ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty);

        var lowered = new SlangTargetLowering().Lower(module);

        Assert.Same(variable, Assert.Single(lowered.Declarations.OfType<VariableDeclaration>()));
    }

    [Fact]
    public void FieldTargetedPropertyMetadataIsRejectedAtRootDiscovery()
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            Parse(new FieldTargetedPropertyShader()));

        Assert.Contains("compiler-generated backing field", exception.Message);
        Assert.Contains("<Data>k__BackingField", exception.Message);
        Assert.Contains("annotate a field declaration instead", exception.Message);
    }

    [Fact]
    public void FieldTargetedPropertyMetadataIsRejectedByDirectFieldParsing()
    {
        var field = typeof(FieldTargetedPropertyShader).GetField(
            "<Data>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?? throw new InvalidOperationException("Backing field was not found.");

        var exception = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseStaticField(field));

        Assert.Contains("compiler-generated backing field", exception.Message);
        Assert.Contains("<Data>k__BackingField", exception.Message);
    }

    [Theory]
    [InlineData(typeof(AnnotatedOrdinaryFieldShader), "ordinary module field")]
    [InlineData(typeof(AnnotatedPropertyShader), "property")]
    [InlineData(typeof(AnnotatedMethodShader), "method")]
    [InlineData(typeof(AnnotatedParameterShader), "parameter")]
    [InlineData(typeof(AnnotatedReturnShader), "return")]
    public void MisplacedShaderMetadataIsRejected(Type shaderType, string declarationKind)
    {
        var exception = Assert.Throws<NotSupportedException>(() => Parse(Create(shaderType)));

        Assert.StartsWith("Shader module metadata validation rejected ", exception.Message);
        Assert.Contains(declarationKind, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResourceDiscoveryDoesNotRunStaticInitializer()
    {
        StaticInitializerProbe.Executions = 0;

        var module = Parse(default(StaticInitializerShader));

        Assert.Equal(0, StaticInitializerProbe.Executions);
        Assert.Contains(
            module.Declarations.OfType<VariableDeclaration>(),
            declaration => declaration.Name == "Data");
    }

    private static ShaderModuleDeclaration<RawCilFunctionBody> Parse(ISharpShader shader) =>
        new RuntimeReflectionParser().ParseShaderModule(shader);

    private static ISharpShader Create(Type type) =>
        (ISharpShader)(Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"Could not create {type}."));

    [NonShaderMetadata]
    private readonly struct ValidBindingsShader : ISharpShader
    {
        [Uniform, Group(1), Binding(2)]
        private static readonly float First = 0;

        [Uniform, Group(2), Binding(2)]
        private static readonly float SameBindingDifferentGroup = 0;

        [Vertex]
        public static int Entry() => 1;
    }

    private readonly struct MissingGroupShader : ISharpShader
    {
        [Uniform, Binding(2)]
        private static readonly float Data = 0;

        [Vertex]
        public static int Entry() => 1;
    }

    private readonly struct MissingBindingShader : ISharpShader
    {
        [Uniform, Group(1)]
        private static readonly float Data = 0;

        [Vertex]
        public static int Entry() => 1;
    }

    private readonly struct MissingBothCoordinatesShader : ISharpShader
    {
        [Uniform]
        private static readonly float Data = 0;

        [Vertex]
        public static int Entry() => 1;
    }

    private readonly struct NegativeGroupShader : ISharpShader
    {
        [Uniform, Group(-1), Binding(2)]
        private static readonly float Data = 0;

        [Vertex]
        public static int Entry() => 1;
    }

    private readonly struct NegativeBindingShader : ISharpShader
    {
        [Uniform, Group(1), Binding(-1)]
        private static readonly float Data = 0;

        [Vertex]
        public static int Entry() => 1;
    }

    private readonly struct DuplicateBindingShader : ISharpShader
    {
        [Uniform, Group(1), Binding(2)]
        private static readonly float First = 0;

        [Uniform, Group(1), Binding(2)]
        private static readonly float Second = 0;

        [Vertex]
        public static int Entry() => 1;
    }

    private readonly struct AnnotatedOrdinaryFieldShader : ISharpShader
    {
        [Location(0)]
        private static readonly float Data = 0;

        [Vertex]
        public static int Entry() => 1;
    }

    private readonly struct AnnotatedPropertyShader : ISharpShader
    {
        [Group(0)]
        private static float Data => 0;

        [Vertex]
        public static int Entry() => 1;
    }

    private readonly struct AnnotatedMethodShader : ISharpShader
    {
        [Vertex, Group(0)]
        public static int Entry() => 1;
    }

    private readonly struct AnnotatedParameterShader : ISharpShader
    {
        [Vertex]
        public static int Entry([Group(0)] int value) => value;
    }

    private readonly struct AnnotatedReturnShader : ISharpShader
    {
        [Vertex]
        [return: Binding(0)]
        public static int Entry() => 1;
    }

    private static class StaticInitializerProbe
    {
        public static int Executions;

        public static float Run()
        {
            Executions++;
            return 1;
        }
    }

    private readonly struct StaticInitializerShader : ISharpShader
    {
        [Uniform, Group(0), Binding(0)]
        private static readonly float Data = StaticInitializerProbe.Run();

        [Vertex]
        public static int Entry() => 1;
    }

    private readonly struct VisibilityHintsShader : ISharpShader
    {
        [Uniform, Group(1), Binding(2), Vertex, Fragment, Compute]
        private static readonly float Data = 0;

        [Vertex]
        public static int Entry() => 1;
    }

    [Group(0)]
    private sealed class AnnotatedShaderType : ISharpShader
    {
        [Vertex]
        public static int Entry() => 1;
    }

    [Align(16)]
    private struct AnnotatedPayload
    {
        public float Value;
    }

    private sealed class AnnotatedPayloadShader : ISharpShader
    {
        [Vertex]
        public static float Entry(AnnotatedPayload value) => value.Value;
    }

    [Align(16)]
    private sealed class AnnotatedReferencePayload;

    private readonly struct AnnotatedReferencePayloadShader : ISharpShader
    {
        [Vertex]
        public static int Entry(AnnotatedReferencePayload value) => 1;
    }

    private readonly struct FieldTargetedPropertyShader : ISharpShader
    {
        [field: Uniform, Group(0), Binding(0)]
        private static float Data { get; }

        [Vertex]
        public static int Entry() => 1;
    }

    [AttributeUsage(AttributeTargets.Struct)]
    private sealed class NonShaderMetadataAttribute : Attribute;
}

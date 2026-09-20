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
}

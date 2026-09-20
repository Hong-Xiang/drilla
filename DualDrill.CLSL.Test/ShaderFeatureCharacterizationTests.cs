using System.Numerics;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.Mathematics;
using Xunit.Abstractions;

namespace DualDrill.CLSL.Test;

[Collection(SlangProcessTestCollection.Name)]
public sealed class ShaderFeatureCharacterizationTests(ITestOutputHelper output)
{
    [Fact]
    public void EmptyComputeEntryIsRejectedBySlangEmitter()
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(new EmptyComputeShader()));

        output.WriteLine(exception.Message);
        Assert.Equal(
            "Slang attribute DualDrill.CLSL.Language.ShaderAttribute.ComputeAttribute is not supported.",
            exception.Message);
    }

    [Fact]
    public void InstanceIndexBuiltinIsRejectedBySlangEmitter()
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(new InstanceIndexShader()));

        output.WriteLine(exception.Message);
        Assert.Equal("Unsupported Slang builtin binding instance_index.", exception.Message);
    }

    [Fact]
    public void OrdinaryIntArrayIndexingIsRejectedDuringPreStackAnalysis()
    {
        var method = ((Func<int[], int, int>)ReadElement).Method;
        var arrayType = Assert.IsType<OpaqueType>(
            new RuntimeReflectionParser().ParseType(typeof(int[])));
        Assert.Equal(typeof(int[]), arrayType.Type);

        var exception = Assert.Throws<ValidationException>(() =>
            CompilerTestPipeline.CompileBody(method));

        output.WriteLine(exception.Message);
        Assert.Matches(
            "^reachable instruction semantics are not supported at IL_[0-9A-F]{4} " +
            "\\(ldelem\\.i4\\)\\. @ ReadElement$",
            exception.Message);
        Assert.IsType<NotImplementedException>(exception.InnerException);
    }

    [Fact]
    public void LegacyTextureSampleHasNoRegisteredIntrinsic()
    {
        var parser = new RuntimeReflectionParser();
        var textureType = Assert.IsType<OpaqueType>(
            parser.ParseType(typeof(ITexture2D<Vector4>)));
        var samplerType = Assert.IsType<OpaqueType>(parser.ParseType(typeof(ISampler)));
        Assert.Equal(typeof(ITexture2D<Vector4>), textureType.Type);
        Assert.Equal(typeof(ISampler), samplerType.Type);

        var method = ((Func<ITexture2D<Vector4>, ISampler, Vector2, Vector4>)Sample).Method;
        var exception = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseMethod(method));

        output.WriteLine(exception.Message);
        output.WriteLine(exception.InnerException?.Message);
        Assert.Matches(
            "^Failed to collect metadata operand at IL_[0-9A-F]{4} \\(callvirt\\) in " +
            "System\\.Numerics\\.Vector4 Sample\\(DualDrill\\.CLSL\\.ITexture2D`1\\[System\\.Numerics\\.Vector4\\], " +
            "DualDrill\\.CLSL\\.ISampler, System\\.Numerics\\.Vector2\\)\\.$",
            exception.Message);
        Assert.Equal(
            "Referenced method System.Numerics.Vector4 Sample(DualDrill.CLSL.ISampler, " +
            "System.Numerics.Vector2) has no decodable CIL body and is not a registered builtin or intrinsic.",
            exception.InnerException?.Message);
    }

    [Fact]
    public void StructMemberSemanticsAreCollectedThenDroppedFromSlang()
    {
        var shader = new StructInterfaceShader();
        var compiler = new CLSLCompiler(new(CLSLCompileTarget.SLang));
        var module = compiler.Parse(shader);
        var structure = Assert.Single(
            module.Declarations.OfType<StructureDeclaration>(),
            declaration => declaration.Name == nameof(VertexOut));

        var position = Assert.Single(structure.Members, member => member.Name == nameof(VertexOut.Position));
        Assert.Equal(
            BuiltinBinding.position,
            Assert.Single(position.Attributes.OfType<BuiltinAttribute>()).Slot);
        var uv = Assert.Single(structure.Members, member => member.Name == nameof(VertexOut.Uv));
        Assert.Equal(0, Assert.Single(uv.Attributes.OfType<LocationAttribute>()).Binding);

        var slang = compiler.Emit(shader);
        output.WriteLine(slang);
        var memberLines = slang.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.EndsWith("Position;", StringComparison.Ordinal) ||
                           line.EndsWith("Uv;", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(["vec4<f32> Position;", "vec2<f32> Uv;"], memberLines);
        Assert.DoesNotContain("SV_POSITION", slang);
        Assert.DoesNotContain("TEXCOORD0", slang);
    }

    private static int ReadElement(int[] values, int index) => values[index];

    private static Vector4 Sample(
        ITexture2D<Vector4> texture,
        ISampler sampler,
        Vector2 uv) =>
        texture.Sample(sampler, uv);

    private sealed class EmptyComputeShader : ISharpShader
    {
        [Compute]
        public static void cs()
        {
        }
    }

    private sealed class InstanceIndexShader : ISharpShader
    {
        [Vertex]
        [return: Builtin(BuiltinBinding.position)]
        public static vec4f32 vs([Builtin(BuiltinBinding.instance_index)] uint index) =>
            DMath.vec4(0f, 0f, 0f, 1f);
    }

    private struct VertexOut
    {
        [Builtin(BuiltinBinding.position)]
        public vec4f32 Position;

        [Location(0)]
        public vec2f32 Uv;
    }

    private sealed class StructInterfaceShader : ISharpShader
    {
        [Vertex]
        public static VertexOut vs(VertexOut value) => value;
    }
}

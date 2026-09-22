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
    public void ComputeEntryWithoutWorkgroupSizeIsRejectedByMetadataValidation()
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(new MissingWorkgroupComputeShader()));

        output.WriteLine(exception.Message);
        Assert.Equal(
            "Shader module metadata validation rejected method " +
            $"'{typeof(MissingWorkgroupComputeShader).FullName}.cs': " +
            "a compute entry requires exactly one [WorkgroupSize] attribute; found 0.",
            exception.Message);
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
    public void StructMemberSemanticsAreRejectedBeforeSlangEmission()
    {
        var shader = new StructInterfaceShader();
        var compiler = new CLSLCompiler(new(CLSLCompileTarget.SLang));
        var exception = Assert.Throws<NotSupportedException>(() => compiler.Emit(shader));

        output.WriteLine(exception.Message);
        Assert.Equal(
            $"Shader module metadata validation rejected field '{typeof(VertexOut).FullName}.Position': " +
            "attribute(s) [Builtin] are not valid on an ordinary module field.",
            exception.Message);
    }

    [Fact]
    public void StructMemberLocationIsRejectedBeforeSlangEmission()
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(new StructLocationShader()));

        output.WriteLine(exception.Message);
        Assert.Equal(
            $"Shader module metadata validation rejected field '{typeof(LocationOnly).FullName}.Uv': " +
            "attribute(s) [Location] are not valid on an ordinary module field.",
            exception.Message);
    }

    [Fact]
    public void StructMemberExplicitAlignmentIsRejectedBeforeSlangEmission()
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(new StructAlignmentShader()));

        output.WriteLine(exception.Message);
        Assert.Equal(
            $"Shader module metadata validation rejected field '{typeof(ExplicitlyAligned).FullName}.Value': " +
            "attribute(s) [Align] are not valid on an ordinary module field.",
            exception.Message);
    }

    private static int ReadElement(int[] values, int index) => values[index];

    private static Vector4 Sample(
        ITexture2D<Vector4> texture,
        ISampler sampler,
        Vector2 uv) =>
        texture.Sample(sampler, uv);

    private sealed class MissingWorkgroupComputeShader : ISharpShader
    {
        [Compute]
        public static void cs()
        {
        }
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

    private struct LocationOnly
    {
        [Location(0)]
        public vec2f32 Uv;
    }

    private sealed class StructLocationShader : ISharpShader
    {
        [Vertex]
        public static LocationOnly vs(LocationOnly value) => value;
    }

    private struct ExplicitlyAligned
    {
        [Align(16)]
        public float Value;
    }

    private sealed class StructAlignmentShader : ISharpShader
    {
        [Vertex]
        public static ExplicitlyAligned vs(ExplicitlyAligned value) => value;
    }
}

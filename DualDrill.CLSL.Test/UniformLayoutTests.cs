using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Reflection;
using DualDrill.CLSL.Test.ShaderModule;
using DualDrill.Graphics;
using DualDrill.Mathematics;
using System.Numerics;
using static DualDrill.Mathematics.DMath;

namespace DualDrill.CLSL.Test;

public sealed class UniformLayoutTests
{
    [Fact]
    public void ReferenceLayoutExposesTargetAbiAndDescriptorProjections()
    {
        var module = new CLSLCompiler(new(CLSLCompileTarget.IR))
            .Parse(new UniformLayoutReferenceShaderModule());
        IShaderModuleReflection reflection = new ShaderModuleReflection();

        var uniforms = reflection.GetUniformBindings(module);
        var settings = Assert.Single(uniforms, uniform => uniform.Name == "Settings");
        Assert.Equal(1, settings.Group);
        Assert.Equal(2, settings.Binding);
        Assert.Equal(GPUBufferBindingType.Uniform, settings.Kind);
        Assert.Equal(GPUShaderStage.Vertex | GPUShaderStage.Fragment, settings.Visibility);
        Assert.True(settings.HasDynamicOffset);
        Assert.Equal(16u, settings.Layout.Alignment);
        Assert.Equal(32u, settings.Layout.Size);
        Assert.Equal(
            new[]
            {
                new ShaderBufferMemberLayout("Tint", 0, 16, 16, 16),
                new ShaderBufferMemberLayout("Exposure", 16, 4, 16, 4),
                new ShaderBufferMemberLayout("Mode", 20, 4, 4, 4),
                new ShaderBufferMemberLayout("Padding", 24, 8, 8, 8)
            },
            settings.Layout.Members.ToArray());

        var extra = Assert.Single(uniforms, uniform => uniform.Name == "Extra");
        Assert.Equal(
            new[]
            {
                new ShaderBufferMemberLayout("Direction", 0, 16, 16, 12),
                new ShaderBufferMemberLayout("Weight", 12, 4, 4, 4),
                new ShaderBufferMemberLayout("Offset", 16, 8, 16, 8)
            },
            extra.Layout.Members.ToArray());
        Assert.Equal(32u, extra.Layout.Size);
        Assert.Equal(
            GPUShaderStage.Vertex | GPUShaderStage.Fragment | GPUShaderStage.Compute,
            extra.Visibility);

        var descriptor = reflection.GetBindGroupLayoutDescriptor(module, 1);
        var entries = descriptor.Entries.ToArray();
        Assert.Equal([2, 3], entries.Select(entry => entry.Binding));
        Assert.Equal([32ul, 32ul], entries.Select(entry => entry.Buffer.MinBindingSize));
        Assert.True(entries[0].Buffer.HasDynamicOffset);
        Assert.False(entries[1].Buffer.HasDynamicOffset);

        var bufferDescriptor = reflection.GetBindGroupLayoutDescriptorBuffer(module, 1);
        Assert.Equal(
            entries.Select(entry => (entry.Binding, entry.Visibility, entry.Buffer)),
            bufferDescriptor.Entries.ToArray()
                            .Select(entry => (entry.Binding, entry.Visibility, entry.Buffer)));
    }

    [Fact]
    public void ProfileCoversScalarVectorFamiliesAndStructurePadding()
    {
        var module = new CLSLCompiler(new(CLSLCompileTarget.IR))
            .Parse(new UniformLayoutProfileShaderModule());
        var uniforms = new ShaderModuleReflection().GetUniformBindings(module);

        AssertDirect(uniforms, "F32", 4, 4);
        AssertDirect(uniforms, "F32x2", 8, 8);
        AssertDirect(uniforms, "F32x3", 16, 12);
        AssertDirect(uniforms, "F32x4", 16, 16);
        AssertDirect(uniforms, "I32", 4, 4);
        AssertDirect(uniforms, "I32x2", 8, 8);
        AssertDirect(uniforms, "I32x3", 16, 12);
        AssertDirect(uniforms, "I32x4", 16, 16);
        AssertDirect(uniforms, "U32", 4, 4);
        AssertDirect(uniforms, "U32x2", 8, 8);
        AssertDirect(uniforms, "U32x3", 16, 12);
        AssertDirect(uniforms, "U32x4", 16, 16);

        var scalar = Assert.Single(uniforms, uniform => uniform.Name == "ScalarStruct");
        Assert.Equal(16u, scalar.Layout.Alignment);
        Assert.Equal(16u, scalar.Layout.Size);
        Assert.Equal(
            new[] { new ShaderBufferMemberLayout("Value", 0, 4, 16, 4) },
            scalar.Layout.Members.ToArray());

        var packed = Assert.Single(uniforms, uniform => uniform.Name == "PackedStruct");
        Assert.Equal(16u, packed.Layout.Size);
        Assert.Equal(
            new[]
            {
                new ShaderBufferMemberLayout("Vector", 0, 16, 16, 12),
                new ShaderBufferMemberLayout("Scalar", 12, 4, 4, 4)
            },
            packed.Layout.Members.ToArray());
        Assert.Equal(
            ["Vector", "Scalar"],
            packed.Layout.Members.Select(member => member.Name));
    }

    [Fact]
    public void DescriptorRequiresNonnegativeGroupAndAllowsAbsentGroup()
    {
        var module = new CLSLCompiler(new(CLSLCompileTarget.IR))
            .Parse(new UniformLayoutReferenceShaderModule());
        IShaderModuleReflection reflection = new ShaderModuleReflection();

        Assert.Empty(reflection.GetBindGroupLayoutDescriptor(module, 0).Entries.ToArray());
        Assert.Empty(reflection.GetBindGroupLayoutDescriptorBuffer(module, 0).Entries.ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(
            () => reflection.GetBindGroupLayoutDescriptor(module, -1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => reflection.GetBindGroupLayoutDescriptorBuffer(module, -1));
    }

    public static TheoryData<string, ISharpShader> UnsupportedUniforms =>
        new()
        {
            { "bool", new UnsupportedUniformShaders.Shader<bool>() },
            { "i8", new UnsupportedUniformShaders.Shader<sbyte>() },
            { "u8", new UnsupportedUniformShaders.Shader<byte>() },
            { "i16", new UnsupportedUniformShaders.Shader<short>() },
            { "u16", new UnsupportedUniformShaders.Shader<ushort>() },
            { "f16", new UnsupportedUniformShaders.Shader<Half>() },
            { "i64", new UnsupportedUniformShaders.Shader<long>() },
            { "u64", new UnsupportedUniformShaders.Shader<ulong>() },
            { "f64", new UnsupportedUniformShaders.Shader<double>() },
            { "array", new UnsupportedUniformShaders.Shader<float[]>() },
            { "matrix", new UnsupportedUniformShaders.Shader<Matrix4x4>() },
            { "opaque", new UnsupportedUniformShaders.Shader<string>() },
            { "nested", new UnsupportedUniformShaders.Shader<UnsupportedUniformShaders.NestedOuter>() },
            { "empty", new UnsupportedUniformShaders.Shader<UnsupportedUniformShaders.Empty>() },
            { "property", new UnsupportedUniformShaders.Shader<UnsupportedUniformShaders.Property>() },
            { "member align", new UnsupportedUniformShaders.Shader<UnsupportedUniformShaders.MemberAlign>() },
            { "structure align", new UnsupportedUniformShaders.Shader<UnsupportedUniformShaders.StructureAlign>() },
            { "explicit layout", new UnsupportedUniformShaders.Shader<UnsupportedUniformShaders.Explicit>() },
            { "variable align", new UnsupportedUniformShaders.VariableAlign() }
        };

    [Theory]
    [MemberData(nameof(UnsupportedUniforms))]
    public void PublicEmissionRejectsUnsupportedUniformsBeforeTargetText(string profile, ISharpShader shader)
    {
        var exception = Assert.Throws<NotSupportedException>(
            () => new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(shader));

        var metadataDiagnostic = profile switch
        {
            "member align" =>
                $"Shader module metadata validation rejected field " +
                $"'{typeof(UnsupportedUniformShaders.MemberAlign).FullName}.Field': " +
                "attribute(s) [Align] are not valid on an ordinary module field.",
            "structure align" =>
                "Shader module metadata validation rejected structure 'StructureAlign': " +
                "attribute(s) [Align] are not supported.",
            "variable align" =>
                $"Shader module metadata validation rejected field " +
                $"'{typeof(UnsupportedUniformShaders.VariableAlign).FullName}.Value': " +
                "resource attribute(s) [Align] are not supported.",
            _ => null
        };
        if (metadataDiagnostic is not null)
        {
            Assert.Equal(metadataDiagnostic, exception.Message);
            return;
        }

        Assert.Contains("Uniform", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WGSL uniform layout profile", exception.Message);
    }

    [Fact]
    public void ParsingAndEmissionDoNotExecuteUniformInitializers()
    {
        initializerCalls = 0;

        _ = new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(default(InitializerShader));

        Assert.Equal(0, initializerCalls);
    }

    private static int initializerCalls;

    private static float TrackInitializer()
    {
        initializerCalls++;
        return 1.0f;
    }

    private struct InitializerShader : ISharpShader
    {
        [Group(0)]
        [Binding(0)]
        [Uniform]
        private static readonly float Value = TrackInitializer();

        [Fragment]
        [return: Location(0)]
        public static vec4f32 Shade() => vec4(0.0f);
    }

    private static void AssertDirect(
        IEnumerable<ShaderUniformBinding> uniforms,
        string name,
        uint alignment,
        uint size)
    {
        var uniform = Assert.Single(uniforms, candidate => candidate.Name == name);
        Assert.Equal(alignment, uniform.Layout.Alignment);
        Assert.Equal(size, uniform.Layout.Size);
        Assert.Empty(uniform.Layout.Members);
    }
}

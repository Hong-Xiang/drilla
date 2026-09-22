using System.Collections.Immutable;
using System.Numerics;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Reflection;
using DualDrill.CLSL.Test.ShaderModule;
using DualDrill.Graphics;
using DualDrill.Mathematics;
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

    [Fact]
    public void TypedReflectionUsesSharedModuleMetadataValidation()
    {
        Assert.Equal(
            "Shader module metadata validation rejected resource 'Data': a resource requires exactly one " +
            "address-space, [Group], and [Binding] attribute (found 1, 0, and 1).",
            Assert.Throws<NotSupportedException>(() =>
                new ShaderModuleReflection().GetUniformBindings(Module(
                    Uniform("Data", UniformAddressSpace.Instance, new UniformAttribute(), new BindingAttribute(2)))))
                .Message);
        Assert.Equal(
            "Shader module metadata validation rejected resource 'NegativeGroup': " +
            "group must be nonnegative; found -1.",
            Assert.Throws<NotSupportedException>(() =>
                new ShaderModuleReflection().GetUniformBindings(Module(
                    Uniform(
                        "NegativeGroup",
                        UniformAddressSpace.Instance,
                        new UniformAttribute(),
                        new GroupAttribute(-1),
                        new BindingAttribute(2)))))
                .Message);
        Assert.Equal(
            "Shader module metadata validation rejected resource 'NegativeBinding': " +
            "binding must be nonnegative; found -1.",
            Assert.Throws<NotSupportedException>(() =>
                new ShaderModuleReflection().GetUniformBindings(Module(
                    Uniform(
                        "NegativeBinding",
                        UniformAddressSpace.Instance,
                        new UniformAttribute(),
                        new GroupAttribute(1),
                        new BindingAttribute(-1)))))
                .Message);
        Assert.Equal(
            "Shader module metadata validation rejected module: resource binding (1, 2) is duplicated by " +
            "'First' and 'Second'.",
            Assert.Throws<NotSupportedException>(() =>
                new ShaderModuleReflection().GetBindGroupLayoutDescriptor(
                    Module(
                        Uniform(
                            "First",
                            UniformAddressSpace.Instance,
                            new UniformAttribute(),
                            new GroupAttribute(1),
                            new BindingAttribute(2)),
                        Uniform(
                            "Second",
                            UniformAddressSpace.Instance,
                            new UniformAttribute(),
                            new GroupAttribute(1),
                            new BindingAttribute(2))),
                    1))
                .Message);
        Assert.Equal(
            "Shader module metadata validation rejected resource 'Mismatch': declared address space Function " +
            "does not match attribute address space Uniform.",
            Assert.Throws<NotSupportedException>(() =>
                new ShaderModuleReflection().GetBindGroupLayoutDescriptorBuffer(
                    Module(
                        Uniform(
                            "Mismatch",
                            FunctionAddressSpace.Instance,
                            new UniformAttribute(),
                            new GroupAttribute(1),
                            new BindingAttribute(2))),
                    1))
                .Message);
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
            { "explicit layout", new UnsupportedUniformShaders.Shader<UnsupportedUniformShaders.Explicit>() }
        };

    [Theory]
    [MemberData(nameof(UnsupportedUniforms))]
    public void PublicEmissionRejectsUnsupportedUniformsBeforeTargetText(string _, ISharpShader shader)
    {
        var exception = Assert.Throws<NotSupportedException>(
            () => new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(shader));

        Assert.Contains("Uniform", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WGSL uniform layout profile", exception.Message);
    }

    public static TheoryData<ISharpShader, string> UnsupportedAlignmentMetadata =>
        new()
        {
            {
                new UnsupportedUniformShaders.Shader<UnsupportedUniformShaders.MemberAlign>(),
                $"Shader module metadata validation rejected field " +
                $"'{typeof(UnsupportedUniformShaders.MemberAlign).FullName}.Field': " +
                "attribute(s) [Align] are not valid on an ordinary module field."
            },
            {
                new UnsupportedUniformShaders.Shader<UnsupportedUniformShaders.StructureAlign>(),
                "Shader module metadata validation rejected structure 'StructureAlign': " +
                "attribute(s) [Align] are not supported."
            },
            {
                new UnsupportedUniformShaders.VariableAlign(),
                $"Shader module metadata validation rejected field " +
                $"'{typeof(UnsupportedUniformShaders.VariableAlign).FullName}.Value': " +
                "resource attribute(s) [Align] are not supported."
            }
        };

    [Theory]
    [MemberData(nameof(UnsupportedAlignmentMetadata))]
    public void PublicEmissionRejectsAlignmentMetadataAtMetadataBoundary(
        ISharpShader shader,
        string expected)
    {
        var exception = Assert.Throws<NotSupportedException>(
            () => new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(shader));

        Assert.Equal(expected, exception.Message);
    }

    [Fact]
    public void ParsingAndEmissionDoNotExecuteUniformInitializers()
    {
        initializerCalls = 0;

        _ = new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(default(InitializerShader));

        Assert.Equal(0, initializerCalls);
    }

    private static int initializerCalls;

    private static VariableDeclaration Uniform(
        string name,
        IAddressSpace addressSpace,
        params IShaderAttribute[] attributes) =>
        new(addressSpace, name, ShaderType.F32, attributes.ToImmutableHashSet());

    private static ShaderModuleDeclaration<RegionFunctionBody> Module(
        params VariableDeclaration[] variables) =>
        new([.. variables], ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty);

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

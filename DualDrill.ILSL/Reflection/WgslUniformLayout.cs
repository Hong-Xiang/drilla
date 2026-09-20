using System.Collections.Immutable;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using DualDrill.Graphics;

namespace DualDrill.CLSL.Reflection;

public sealed record ShaderBufferMemberLayout(
    string Name,
    uint Offset,
    uint NaturalAlignment,
    uint EffectiveAlignment,
    uint Size);

public sealed record ShaderBufferLayout(
    uint Alignment,
    uint Size,
    ImmutableArray<ShaderBufferMemberLayout> Members);

public sealed record ShaderUniformBinding(
    string Name,
    int Group,
    int Binding,
    GPUShaderStage Visibility,
    bool HasDynamicOffset,
    ShaderBufferLayout Layout)
{
    public GPUBufferBindingType Kind => GPUBufferBindingType.Uniform;
}

internal static class WgslUniformLayoutCalculator
{
    private const uint StructureAlignment = 16;

    public static ShaderBufferLayout Calculate(VariableDeclaration uniform)
    {
        if (uniform.Attributes.OfType<AlignAttribute>().Any())
            throw Unsupported(
                uniform.Name,
                uniform.Type,
                "explicit alignment on a uniform declaration is not supported");

        return Calculate(uniform.Type, uniform.Name);
    }

    public static ShaderBufferLayout Calculate(IShaderType type, string uniformName) =>
        type switch
        {
            StructureType structure => CalculateStructure(structure, uniformName),
            _ => CalculateDirect(type, uniformName)
        };

    private static ShaderBufferLayout CalculateDirect(IShaderType type, string uniformName)
    {
        var (alignment, size) = GetScalarOrVectorLayout(type, uniformName);
        return new ShaderBufferLayout(alignment, size, []);
    }

    private static ShaderBufferLayout CalculateStructure(StructureType structure, string uniformName)
    {
        var declaration = structure.Declaration;
        if (declaration.Attributes.OfType<AlignAttribute>().Any())
            throw Unsupported(uniformName, structure, "explicit structure alignment is not supported");
        if (declaration.Members.IsEmpty)
            throw Unsupported(uniformName, structure, "empty uniform structures are not supported");

        var members = ImmutableArray.CreateBuilder<ShaderBufferMemberLayout>(declaration.Members.Length);
        uint offset = 0;
        foreach (var member in declaration.Members)
        {
            if (member.Attributes.OfType<AlignAttribute>().Any())
                throw Unsupported(
                    uniformName,
                    structure,
                    $"explicit alignment on member '{member.Name}' is not supported");
            if (member.Type is StructureType)
                throw Unsupported(
                    uniformName,
                    structure,
                    $"nested structure member '{member.Name}' is not supported");

            var (naturalAlignment, size) = GetScalarOrVectorLayout(
                member.Type,
                $"{uniformName}.{member.Name}");
            offset = RoundUp(offset, naturalAlignment);
            // Slang starts each 16-byte uniform register with an explicit stronger alignment.
            var effectiveAlignment = offset % StructureAlignment == 0
                ? Math.Max(StructureAlignment, naturalAlignment)
                : naturalAlignment;
            members.Add(new ShaderBufferMemberLayout(
                member.Name,
                offset,
                naturalAlignment,
                effectiveAlignment,
                size));
            offset += size;
        }

        return new ShaderBufferLayout(
            StructureAlignment,
            RoundUp(offset, StructureAlignment),
            members.MoveToImmutable());
    }

    private static (uint Alignment, uint Size) GetScalarOrVectorLayout(
        IShaderType type,
        string uniformName) =>
        type switch
        {
            FloatType<N32> or IntType<N32> or UIntType<N32> => (4, 4),
            IVecType vector => GetVectorLayout(vector, uniformName),
            _ => throw Unsupported(
                uniformName,
                type,
                "only f32, i32, u32, and their 2-, 3-, or 4-component vectors are supported")
        };

    private static (uint Alignment, uint Size) GetVectorLayout(
        IVecType vector,
        string uniformName)
    {
        if (vector.ElementType is not (FloatType<N32> or IntType<N32> or UIntType<N32>))
            throw Unsupported(
                uniformName,
                vector,
                "only f32, i32, and u32 vector elements are supported");

        return vector.Size.Value switch
        {
            2 => (8, 8),
            3 => (16, 12),
            4 => (16, 16),
            _ => throw Unsupported(
                uniformName,
                vector,
                "only 2-, 3-, and 4-component vectors are supported")
        };
    }

    private static uint RoundUp(uint value, uint alignment) =>
        checked((value + alignment - 1) / alignment * alignment);

    private static NotSupportedException Unsupported(
        string uniformName,
        IShaderType type,
        string reason) =>
        new($"Uniform '{uniformName}' with shader type '{type.Name}' is outside the WGSL uniform layout profile: " +
            reason + ".");
}

internal static class WgslUniformLayoutValidator
{
    public static void Validate(IShaderModuleDeclaration module)
    {
        foreach (var uniform in module.Declarations
                     .OfType<VariableDeclaration>()
                     .Where(declaration => declaration.Attributes.OfType<UniformAttribute>().Any()))
            _ = WgslUniformLayoutCalculator.Calculate(uniform);
    }
}

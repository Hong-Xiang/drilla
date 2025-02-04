using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.Declaration;

public sealed class StructureDeclaration : IShaderType, IDeclaration
{
    public required string Name { get; init; }
    public ImmutableArray<MemberDeclaration> Members { get; set; } = [];
    public ImmutableHashSet<IShaderAttribute> Attributes { get; set; } = [];

    IPtrType? PtrType { get; set; } = null;

    public StructureDeclaration()
    {
    }

    public IRefType GetRefType()
    {
        throw new NotImplementedException();
    }

    public IPtrType GetPtrType()
    {
        PtrType ??= new PtrType(this);
        return PtrType;
    }
}

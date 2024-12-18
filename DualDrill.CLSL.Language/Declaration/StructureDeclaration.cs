using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace DualDrill.CLSL.Language.Declaration;

public sealed record class StructureDeclaration(
    string Name,
    ImmutableArray<MemberDeclaration> Members,
    ImmutableHashSet<IShaderAttribute> Attributes
) : IShaderType, IDeclaration
{
    public IRefType GetRefType()
    {
        throw new NotImplementedException();
    }

    public IPtrType GetPtrType()
    {
        throw new NotImplementedException();
    }
}

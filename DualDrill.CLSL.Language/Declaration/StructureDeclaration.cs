using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.Declaration;

public sealed record class StructureDeclaration(
    string Name,
    ImmutableArray<MemberDeclaration> Members,
    ImmutableHashSet<IShaderAttribute> Attributes
) : IShaderType, IDeclaration
{
    public IRefType RefType => throw new NotImplementedException();

    public IPtrType PtrType => throw new NotImplementedException();
}

using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.IR;

public sealed record class TypeReference(IShaderType Type) : IShaderAstNode
{
}

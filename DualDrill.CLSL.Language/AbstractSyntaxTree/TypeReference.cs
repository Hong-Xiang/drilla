using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree;

public sealed record class TypeReference(IShaderType Type) : IShaderAstNode
{
}

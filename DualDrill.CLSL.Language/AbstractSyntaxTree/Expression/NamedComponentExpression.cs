using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public sealed record class NamedComponentExpression(IExpression Base, string ComponentName) : IExpression
{
    public IShaderType Type { get; } = ((StructureDeclaration)(Base.Type)).Members.Single(m => m.Name == ComponentName).Type;
}

using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Literal;

public readonly record struct BoolLiteral(bool Value) : ILiteral
{
    public IShaderType Type => ShaderType.Bool;
    public override string ToString() => $"{Type.Name}({Value})";
}

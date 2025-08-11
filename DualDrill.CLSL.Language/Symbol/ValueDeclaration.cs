using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Symbol;

public readonly record struct ValueDeclaration(ShaderValue Value, IShaderType Type)
{
}
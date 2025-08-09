using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Region;

public sealed record class RegionParameter(ShaderValue Value, IShaderType Type)
{
}

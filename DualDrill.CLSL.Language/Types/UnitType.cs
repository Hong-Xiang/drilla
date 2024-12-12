using DualDrill.Common;

namespace DualDrill.CLSL.Language.Types;

public sealed record class UnitType : IShaderType, ISingleton<UnitType>
{
    public static UnitType Instance { get; } = new();

    public string Name => "Unit";
}

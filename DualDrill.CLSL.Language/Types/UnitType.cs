using DotNext.Patterns;

namespace DualDrill.CLSL.Language.Types;

public sealed class UnitType : ISingletonShaderType<UnitType>, ISingleton<UnitType>
{
    private UnitType() { }
    public static UnitType Instance { get; } = new();

    public string Name => "Unit";

    public IRefType GetRefType()
    {
        throw new NotSupportedException();
    }

    public IPtrType GetPtrType()
    {
        throw new NotSupportedException();
    }
}

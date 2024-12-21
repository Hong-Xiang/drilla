using DotNext.Patterns;
using System.Diagnostics;

namespace DualDrill.CLSL.Language.Types;

[DebuggerDisplay("{Name}")]
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

using DotNext.Patterns;
using System.Diagnostics;

namespace DualDrill.CLSL.Language.Types;

[DebuggerDisplay("{Name}")]
public sealed class UnitType : ISingletonShaderType<UnitType>, ISingleton<UnitType>
{
    private UnitType() { }
    public static UnitType Instance { get; } = new();

    public string Name => "Unit";

    T IShaderType.Evaluate<T>(IShaderTypeSemantic<T, T> semantic)
    {
        return semantic.UnitType(this);
    }

    public IRefType GetRefType()
    {
        throw new NotSupportedException();
    }

    public IPtrType GetPtrType()
    {
        throw new NotSupportedException();
    }
}

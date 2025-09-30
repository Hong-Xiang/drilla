using System.Diagnostics;
using DotNext.Patterns;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language.Types;

[DebuggerDisplay("{Name}")]
public sealed class UnitType : ISingletonShaderType<UnitType>, ISingleton<UnitType>
{
    private UnitType()
    {
    }

    public static UnitType Instance { get; } = new();

    public string Name => "Unit";

    T IShaderType.Evaluate<T>(IShaderTypeSemantic<T, T> semantic) => semantic.UnitType(this);

    public IRefType GetRefType() => throw new NotSupportedException();

    public IPtrType GetPtrType(IAddressSpace? addressSpace) => throw new NotSupportedException();
}
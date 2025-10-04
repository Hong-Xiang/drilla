using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language.Types;

/// <summary>
///     Special type for C#'s type place holder, useful for methods argument which is not actually used
/// </summary>
/// <param name="Type"></param>
public sealed class OpaqueType : IShaderType<OpaqueType>
{
    public OpaqueType(Type? type)
    {
        Type = type;
    }

    public Type? Type { get; }

    public string Name => $"<OpaqueType:{Type?.Name ?? nameof(OpaqueType)}>";

    public IRefType GetRefType() => throw new NotSupportedException();

    public IPtrType GetPtrType(IAddressSpace addressSpace) => throw new NotImplementedException();

    public T Evaluate<T>(IShaderTypeSemantic<T, T> semantic) => throw new NotImplementedException();

    public static OpaqueType Instance => throw new NotImplementedException();
}
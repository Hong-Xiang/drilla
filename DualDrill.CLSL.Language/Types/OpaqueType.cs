namespace DualDrill.CLSL.Language.Types;

/// <summary>
/// Special type for C#'s type place holder, useful for methods argument which is not actually used
/// </summary>
/// <param name="Type"></param>
public sealed class OpaqueType : IShaderType<OpaqueType>
{
    public Type? Type { get; }

    public OpaqueType(Type? type)
    {
        Type = type;
        PtrType = new(() => new PtrType(this));
    }

    public string Name => $"<OpaqueType:{Type?.Name ?? nameof(OpaqueType)}>";

    public IRefType GetRefType()
    {
        throw new NotSupportedException();
    }

    Lazy<IPtrType> PtrType { get; }
    public IPtrType GetPtrType() => PtrType.Value;
    public static OpaqueType Instance => throw new NotImplementedException();
}
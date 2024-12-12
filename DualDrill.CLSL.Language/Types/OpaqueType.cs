namespace DualDrill.CLSL.Language.Types;

/// <summary>
/// Special type for C#'s type place holder, useful for methods argument which is not actually used
/// </summary>
/// <param name="Type"></param>
public sealed record class OpaqueType(Type? Type) : IShaderType
{
    public string Name => $"<OpaqueType:{Type?.Name ?? nameof(OpaqueType)}>";
}

namespace DualDrill.CLSL.Language.Types;

public interface IRefType : IShaderType { }
public sealed record class RefType<TShaderType>(TShaderType BaseType) : IRefType
    where TShaderType : IShaderType
{
    public string Name => $"ref<{BaseType.Name}>";

    public IPtrType PtrType => throw new NotImplementedException();

    IRefType IShaderType.RefType => throw new NotImplementedException();
}

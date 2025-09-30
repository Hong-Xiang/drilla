using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language.Types;

public interface IRefType : IShaderType
{
}

public sealed record class RefType<TShaderType>(TShaderType BaseType) : IRefType, IShaderType<RefType<TShaderType>>
    where TShaderType : IShaderType
{
    public string Name => $"ref<{BaseType.Name}>";

    public IRefType GetRefType() => throw new NotImplementedException();

    public IPtrType GetPtrType(IAddressSpace addressSpace) => throw new NotImplementedException();

    public T Evaluate<T>(IShaderTypeSemantic<T, T> semantic) => throw new NotImplementedException();

    public static RefType<TShaderType> Instance => throw new NotImplementedException();

    public IPtrType GetPtrType() => throw new NotImplementedException();

    public IUnaryExpressionOperation? CilStackConversion() => null;
}
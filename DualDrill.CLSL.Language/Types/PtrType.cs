using DualDrill.Common;

namespace DualDrill.CLSL.Language.Types;

public interface IPtrType : IShaderType
{
    IShaderType BaseType { get; }
}

public sealed record class PtrType(IShaderType BaseType) : IPtrType
{
    public string Name => $"ptr<{BaseType.Name}>";

    public IRefType GetRefType()
    {
        throw new NotImplementedException();
    }

    public IPtrType GetPtrType()
    {
        throw new NotImplementedException();
    }
}

sealed class SingletonPtrType<TBaseType> : IPtrType, ISingleton<SingletonPtrType<TBaseType>>
    where TBaseType : IShaderType, ISingleton<TBaseType>
{
    public static SingletonPtrType<TBaseType> Instance { get; } = new();
    public IShaderType BaseType => TBaseType.Instance;
    public string Name => $"ptr<{TBaseType.Instance.Name}>";

    public IRefType GetRefType()
    {
        throw new NotSupportedException();
    }

    public IPtrType GetPtrType() => SingletonPtrType<SingletonPtrType<TBaseType>>.Instance;
}
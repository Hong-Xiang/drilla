using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;
using System.Diagnostics;

namespace DualDrill.CLSL.Language.Types;

public interface IPtrType : IShaderType
{
    IShaderType BaseType { get; }
    IAddressSpace AddressSpace { get; }

    public static IPtrType CreateFromSingletonType<TBaseType>(IAddressSpace s)
        where TBaseType : IShaderType, ISingleton<TBaseType>
        => s.EvalG(new PtrTypeGSemantic<TBaseType>());

    sealed class PtrTypeGSemantic<TBaseType> : IAddressSpaceGenericSemantic<IPtrType>
        where TBaseType : IShaderType, ISingleton<TBaseType>
    {
        public IPtrType AddressSpace<TSpace>(TSpace space) where TSpace : IAddressSpace<TSpace>
            => SingletonPtrType<TBaseType, TSpace>.Instance;
    }
}

public sealed record class PtrType(IShaderType BaseType, IAddressSpace AddressSpace) : IPtrType, IShaderType<PtrType>
{
    public string Name => $"ptr<{BaseType.Name}>";

    public IRefType GetRefType()
    {
        throw new NotImplementedException();
    }

    public IPtrType GetPtrType(IAddressSpace? addressSpace)
    {
        throw new NotImplementedException();
    }

    public static PtrType Instance => throw new NotImplementedException();
    public T Evaluate<T>(IShaderTypeSemantic<T, T> semantic)
        => semantic.PtrType(this);
}

sealed class SingletonPtrType<TBaseType> : IPtrType, ISingleton<SingletonPtrType<TBaseType>>,
                                           IShaderType<SingletonPtrType<TBaseType>>
    where TBaseType : IShaderType, ISingleton<TBaseType>
{
    public static SingletonPtrType<TBaseType> Instance { get; } = new();
    public IShaderType BaseType => TBaseType.Instance;
    public string Name => $"ptr<{TBaseType.Instance.Name}>";

    public IAddressSpace AddressSpace => GenericAddressSpace.Instance;

    public IRefType GetRefType()
    {
        throw new NotSupportedException();
    }

    public IPtrType GetPtrType(IAddressSpace? addressSpace) => SingletonPtrType<SingletonPtrType<TBaseType>>.Instance;

    public T Evaluate<T>(IShaderTypeSemantic<T, T> semantic)
        => semantic.PtrType(this);
}

[DebuggerDisplay("{Name}")]
sealed class SingletonPtrType<TBaseType, TAddressSpace>
    : IPtrType
    , ISingleton<SingletonPtrType<TBaseType, TAddressSpace>>
    , IShaderType<SingletonPtrType<TBaseType, TAddressSpace>>
    where TBaseType : IShaderType, ISingleton<TBaseType>
    where TAddressSpace : IAddressSpace<TAddressSpace>
{
    public static SingletonPtrType<TBaseType, TAddressSpace> Instance { get; } = new();
    public IShaderType BaseType => TBaseType.Instance;
    public string Name => $"ptr<{TBaseType.Instance.Name}, {TAddressSpace.Instance.Kind}>";

    public IAddressSpace AddressSpace => TAddressSpace.Instance;

    public IRefType GetRefType()
    {
        throw new NotSupportedException();
    }

    public IPtrType GetPtrType(IAddressSpace addressSpace) =>
        IPtrType.CreateFromSingletonType<SingletonPtrType<TBaseType, TAddressSpace>>(addressSpace);

    public T Evaluate<T>(IShaderTypeSemantic<T, T> semantic)
        => semantic.PtrType(this);
}
﻿using DotNext.Patterns;

namespace DualDrill.CLSL.Language.Types;

public interface IPtrType : IShaderType
{
    IShaderType BaseType { get; }
}

public sealed record class PtrType(IShaderType BaseType) : IPtrType
{
    public string Name => $"ptr<{BaseType.Name}>";

    public IRefType RefType => throw new NotImplementedException();

    IPtrType IShaderType.PtrType => throw new NotImplementedException();
}


sealed class SingletonPtrType<TBaseType>() : IPtrType, ISingleton<SingletonPtrType<TBaseType>>
    where TBaseType : class, IShaderType, ISingleton<TBaseType>
{
    public static SingletonPtrType<TBaseType> Instance { get; } = new();
    public IShaderType BaseType => TBaseType.Instance;
    public string Name => $"ptr<{TBaseType.Instance.Name}>";

    public IRefType RefType
    {
        get
        {
            throw new NotSupportedException();
        }
    }

    public IPtrType PtrType => SingletonPtrType<SingletonPtrType<TBaseType>>.Instance;

}



﻿namespace DualDrill.CLSL.Language.Types;

public interface IRefType : IShaderType { }
public sealed record class RefType<TShaderType>(TShaderType BaseType) : IRefType
    where TShaderType : IShaderType
{
    public string Name => $"ref<{BaseType.Name}>";

    public IPtrType GetPtrType()
    {
        throw new NotImplementedException();
    }

    public IRefType GetRefType()
    {
        throw new NotImplementedException();
    }
}

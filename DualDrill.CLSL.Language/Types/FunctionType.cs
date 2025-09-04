using DualDrill.CLSL.Language.Symbol;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.Types;

public record class FunctionType(
    ImmutableArray<IShaderType> ParameterTypes,
    IShaderType ResultType
) : IShaderType
  , IEquatable<FunctionType>
{
    public string Name => "(" + string.Join(", ", ParameterTypes.Select(t => t.Name)) + " ) -> " + ResultType.Name;

    public TResult Accept<TVisitor, TResult>(TVisitor visitor) where TVisitor : IShaderTypeVisitor1<TResult>
    {
        throw new NotImplementedException();
    }

    public T Evaluate<T>(IShaderTypeSemantic<T, T> semantic)
        => semantic.FunctionType(this);

    public IPtrType GetPtrType(IAddressSpace addressSpace)
    {
        throw new NotImplementedException();
    }

    public IRefType GetRefType()
    {
        throw new NotImplementedException();
    }

    public virtual bool Equals(FunctionType? other)
    {
        return other is not null &&
               ParameterTypes.SequenceEqual(other.ParameterTypes) &&
               ResultType.Equals(other.ResultType);
    }

    public override int GetHashCode()
    {
        var hash = ResultType.GetHashCode();
        foreach (var p in ParameterTypes)
        {
            hash = HashCode.Combine(hash, p.GetHashCode());
        }
        return hash;
    }
}

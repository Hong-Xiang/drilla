using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Operation;
using DualDrill.Common;
using DualDrill.Common.Nat;
using System.Diagnostics;
using static DualDrill.CLSL.Language.Operation.Swizzle;

namespace DualDrill.CLSL.Language.Types;

public interface IVecType : IShaderType
{
    public IScalarType ElementType { get; }
    public IRank Size { get; }

    public IOperation ComponentGetOperation(IComponent c);
    public IOperation ComponentSetOperation(IComponent c);

    public interface IVisitor<TResult>
    {
        public TResult Visit<TRank, TElement>(VecType<TRank, TElement> t)
            where TRank : IRank<TRank>
            where TElement : IScalarType<TElement>;
    }

    public TResult Accept<TResult>(IVisitor<TResult> visitor);
}


public interface IVecType<TSelf> : IVecType, ISingleton<TSelf>
    where TSelf : IVecType<TSelf>
{
    IShaderType SwizzleResultType<TPattern>() where TPattern : Swizzle.IPattern<TPattern>;
}

public interface ISizedVecType<TRank, TSelf> : IVecType<TSelf>
    where TRank : IRank<TRank>
    where TSelf : ISizedVecType<TRank, TSelf>
{
    public interface ISizedVisitor<TResult>
    {
        public TResult Visit<TElement>(VecType<TRank, TElement> t)
            where TElement : IScalarType<TElement>;
    }

    public TResult Accept<TResult>(ISizedVisitor<TResult> visitor);
}

[DebuggerDisplay("{Name}")]
public sealed class VecType<TRank, TElement>
    : ISizedVecType<TRank, VecType<TRank, TElement>>
    , ISingleton<VecType<TRank, TElement>>
    , ISingletonShaderType<VecType<TRank, TElement>>
    where TRank : IRank<TRank>
    where TElement : IScalarType<TElement>
{
    private VecType() { }
    public static VecType<TRank, TElement> Instance { get; } = new();

    public string Name => $"vec{Size.Value}{ElementType.ElementName()}";

    public int ByteSize => Size.Value * ElementType.ByteSize;

    public IScalarType ElementType => TElement.Instance;

    public IRank Size => TRank.Instance;

    public IRefType GetRefType()
    {
        throw new NotImplementedException();
    }

    public IPtrType GetPtrType() => SingletonPtrType<VecType<TRank, TElement>>.Instance;

    public IShaderType SwizzleResultType<TPattern>() where TPattern : Swizzle.IPattern<TPattern>
        => TPattern.Instance.TargetType<TElement>();

    public IOperation ComponentGetOperation(IComponent c)
    {
        if (c is Swizzle.ISizedComponent<TRank> rc)
        {
            return rc.ComponentGetOperation<VecType<TRank, TElement>, TElement>();
        }
        throw new NotSupportedException();
    }

    public IOperation ComponentSetOperation(IComponent c)
    {
        if (c is Swizzle.ISizedComponent<TRank> rc)
        {
            return rc.ComponentSetOperation<VecType<TRank, TElement>, TElement>();
        }
        throw new NotSupportedException();
    }

    public TResult Accept<TResult>(IVecType.IVisitor<TResult> visitor)
        => visitor.Visit(this);

    public TResult Accept<TResult>(ISizedVecType<TRank, VecType<TRank, TElement>>.ISizedVisitor<TResult> visitor)
        => visitor.Visit(this);

    public sealed class ComponentMember<TComponent>
        where TComponent : ISizedComponent<TRank, TComponent>
    {
        public MemberDeclaration Declaration { get; } = new MemberDeclaration(
              TComponent.Instance.Name,
              TElement.Instance,
              []);
    }

    public IEnumerable<IVectorBinaryNumericOperation> GetBinaryNumericOperations()
    {
        yield return VectorNumericBinaryOperation<TRank, TElement, BinaryArithmetic.Add>.Instance;
        yield return VectorNumericBinaryOperation<TRank, TElement, BinaryArithmetic.Sub>.Instance;
        yield return VectorNumericBinaryOperation<TRank, TElement, BinaryArithmetic.Mul>.Instance;
        yield return VectorNumericBinaryOperation<TRank, TElement, BinaryArithmetic.Div>.Instance;
        yield return VectorNumericBinaryOperation<TRank, TElement, BinaryArithmetic.Rem>.Instance;

        yield return ScalarVectorNumericOperation<TRank, TElement, BinaryArithmetic.Add>.Instance;
        yield return ScalarVectorNumericOperation<TRank, TElement, BinaryArithmetic.Sub>.Instance;
        yield return ScalarVectorNumericOperation<TRank, TElement, BinaryArithmetic.Mul>.Instance;
        yield return ScalarVectorNumericOperation<TRank, TElement, BinaryArithmetic.Div>.Instance;
        yield return ScalarVectorNumericOperation<TRank, TElement, BinaryArithmetic.Rem>.Instance;

        yield return VectorScalarNumericOperation<TRank, TElement, BinaryArithmetic.Add>.Instance;
        yield return VectorScalarNumericOperation<TRank, TElement, BinaryArithmetic.Sub>.Instance;
        yield return VectorScalarNumericOperation<TRank, TElement, BinaryArithmetic.Mul>.Instance;
        yield return VectorScalarNumericOperation<TRank, TElement, BinaryArithmetic.Div>.Instance;
        yield return VectorScalarNumericOperation<TRank, TElement, BinaryArithmetic.Rem>.Instance;
    }
}

public static partial class ShaderType
{
    public static IEnumerable<IVecType> GetVecTypes() =>
        [..from r in Ranks
           from e in ScalarTypes
           select GetVecType(r, e)];
    public static IVecType GetVecType(IRank size, IScalarType elementType)
        => size.Accept(new VecTypeFromRankVisitor(elementType));

    private struct VecTypeFromRankVisitor(IScalarType ElementType) : IRank.IVisitor<IVecType>
    {
        readonly IVecType IRank.IVisitor<IVecType>.Visit<TRank>() => ElementType.GetVecType<TRank>();
    }
}

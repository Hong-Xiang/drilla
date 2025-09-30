using System.Diagnostics;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.ShaderAttribute.Metadata;
using DualDrill.Common.Nat;
using static DualDrill.CLSL.Language.Operation.Swizzle;

namespace DualDrill.CLSL.Language.Types;

public interface IVecType : IShaderType
{
    public IScalarType ElementType { get; }
    public IRank Size { get; }

    public FunctionDeclaration ZeroConstructor { get; }
    public IOperation FromScalarConstructOperation { get; }

    public IOperation ComponentGetOperation(IComponent c);
    public IOperation ComponentSetOperation(IComponent c);

    public TResult Accept<TResult>(IVisitor<TResult> visitor);

    public interface IVisitor<TResult>
    {
        public TResult Visit<TRank, TElement>(VecType<TRank, TElement> t)
            where TRank : IRank<TRank>
            where TElement : IScalarType<TElement>;
    }
}

public interface IVecType<TSelf> : IVecType, IShaderType<TSelf>
    where TSelf : IVecType<TSelf>
{
    IShaderType SwizzleResultType<TPattern>() where TPattern : IPattern<TPattern>;
}

public interface ISizedVecType<TRank, TSelf> : IVecType<TSelf>
    where TRank : IRank<TRank>
    where TSelf : ISizedVecType<TRank, TSelf>
{
    public TResult Accept<TResult>(ISizedVisitor<TResult> visitor);

    public interface ISizedVisitor<TResult>
    {
        public TResult Visit<TElement>(VecType<TRank, TElement> t)
            where TElement : IScalarType<TElement>;
    }
}

[DebuggerDisplay("{Name,nq}")]
public sealed class VecType<TRank, TElement>
    : ISizedVecType<TRank, VecType<TRank, TElement>>
    , ISingletonShaderType<VecType<TRank, TElement>>
    where TRank : IRank<TRank>
    where TElement : IScalarType<TElement>
{
    private VecType()
    {
        var ctorName = $"vec{Size.Value}{ElementType.ElementName()}";
        ZeroConstructor = new FunctionDeclaration(
            $"vec{Size.Value}{ElementType.ElementName()}",
            [],
            new FunctionReturn(this, []),
            [new ShaderRuntimeMethodAttribute(), new ZeroConstructorMethodAttribute()]);
    }

    public int ByteSize => Size.Value * ElementType.ByteSize;

    public static VecType<TRank, TElement> Instance { get; } = new();

    public string Name => $"vec{Size.Value}<{ElementType.Name}>";

    public IScalarType ElementType => TElement.Instance;

    public IRank Size => TRank.Instance;

    public IShaderType SwizzleResultType<TPattern>() where TPattern : IPattern<TPattern> =>
        TPattern.Instance.ValueVecType<TElement>();

    public IOperation ComponentGetOperation(IComponent c)
    {
        if (c is ISizedComponent<TRank> rc) return rc.ComponentGetOperation<VecType<TRank, TElement>, TElement>();

        throw new NotSupportedException();
    }

    public IOperation ComponentSetOperation(IComponent c)
    {
        if (c is ISizedComponent<TRank> rc) return rc.ComponentSetOperation<VecType<TRank, TElement>, TElement>();

        throw new NotSupportedException();
    }

    public TResult Accept<TResult>(IVecType.IVisitor<TResult> visitor) => visitor.Visit(this);

    public TResult Accept<TResult>(ISizedVecType<TRank, VecType<TRank, TElement>>.ISizedVisitor<TResult> visitor) =>
        visitor.Visit(this);

    public FunctionDeclaration ZeroConstructor { get; }

    T IShaderType.Evaluate<T>(IShaderTypeSemantic<T, T> semantic) => semantic.VecType(this);

    public IOperation FromScalarConstructOperation =>
        VectorFromScalarConstructOperation<TRank, TElement>.Instance;

    public override string ToString() => Name;

    public IEnumerable<IVectorBinaryNumericOperation> GetBinaryNumericOperations()
    {
        yield return VectorExpressionNumericBinaryExpressionOperation<TRank, TElement, BinaryArithmetic.Add>.Instance;
        yield return VectorExpressionNumericBinaryExpressionOperation<TRank, TElement, BinaryArithmetic.Sub>.Instance;
        yield return VectorExpressionNumericBinaryExpressionOperation<TRank, TElement, BinaryArithmetic.Mul>.Instance;
        yield return VectorExpressionNumericBinaryExpressionOperation<TRank, TElement, BinaryArithmetic.Div>.Instance;
        yield return VectorExpressionNumericBinaryExpressionOperation<TRank, TElement, BinaryArithmetic.Rem>.Instance;

        yield return ScalarVectorExpressionNumericOperation<TRank, TElement, BinaryArithmetic.Add>.Instance;
        yield return ScalarVectorExpressionNumericOperation<TRank, TElement, BinaryArithmetic.Sub>.Instance;
        yield return ScalarVectorExpressionNumericOperation<TRank, TElement, BinaryArithmetic.Mul>.Instance;
        yield return ScalarVectorExpressionNumericOperation<TRank, TElement, BinaryArithmetic.Div>.Instance;
        yield return ScalarVectorExpressionNumericOperation<TRank, TElement, BinaryArithmetic.Rem>.Instance;

        yield return VectorScalarExpressionNumericOperation<TRank, TElement, BinaryArithmetic.Add>.Instance;
        yield return VectorScalarExpressionNumericOperation<TRank, TElement, BinaryArithmetic.Sub>.Instance;
        yield return VectorScalarExpressionNumericOperation<TRank, TElement, BinaryArithmetic.Mul>.Instance;
        yield return VectorScalarExpressionNumericOperation<TRank, TElement, BinaryArithmetic.Div>.Instance;
        yield return VectorScalarExpressionNumericOperation<TRank, TElement, BinaryArithmetic.Rem>.Instance;
    }

    public sealed class ComponentMember<TComponent>
        where TComponent : ISizedComponent<TRank, TComponent>
    {
        public MemberDeclaration Declaration { get; } = new(
            TComponent.Instance.Name,
            TElement.Instance,
            []);
    }
}

public static partial class ShaderType
{
    public static IEnumerable<IVecType> GetVecTypes() =>
    [
        ..from r in Ranks
          from e in ScalarTypes
          select GetVecType(r, e)
    ];

    public static IVecType GetVecType(IRank size, IScalarType elementType) =>
        size.Accept(new VecTypeFromRankVisitor(elementType));

    private struct VecTypeFromRankVisitor(IScalarType ElementType) : IRank.IVisitor<IVecType>
    {
        readonly IVecType IRank.IVisitor<IVecType>.Visit<TRank>() => ElementType.GetVecType<TRank>();
    }
}
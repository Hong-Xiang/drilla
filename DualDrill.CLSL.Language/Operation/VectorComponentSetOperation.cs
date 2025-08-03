using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Language.ValueInstruction;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Operation;

public interface IVectorComponentSetOperation
    : IBinaryStatementOperation
{
}

public interface IVectorComponentSetOperation<TSelf>
    : IVectorComponentSetOperation
    , IBinaryStatementOperation<TSelf>
    where TSelf : IVectorComponentSetOperation<TSelf>
{
    IInstruction IOperation.Instruction => BinaryStatementOperationInstruction<TSelf>.Instance;
}

public sealed class VectorComponentSetOperation<TRank, TVector, TComponent>
    : IVectorComponentSetOperation<VectorComponentSetOperation<TRank, TVector, TComponent>>
    where TRank : IRank<TRank>
    where TVector : ISizedVecType<TRank, TVector>
    where TComponent : Swizzle.ISizedComponent<TRank, TComponent>
{
    public static VectorComponentSetOperation<TRank, TVector, TComponent> Instance { get; } = new();

    public FunctionDeclaration Function { get; } = new(
        $"set_{TComponent.Instance.Name}_{TVector.Instance.Name}",
        [
            new ParameterDeclaration("v", TVector.Instance.GetPtrType(), []),
            new ParameterDeclaration("value", TVector.Instance.ElementType, [])
        ],
        new FunctionReturn(UnitType.Instance, []),
        [new OperationMethodAttribute<VectorComponentSetOperation<TRank, TVector, TComponent>>()]);

    public string Name => $"set.{TComponent.Instance.Name}.{TVector.Instance.Name}";


    record struct SizedVecStmtVisitor(IExpression Target, IExpression Value)
        : ISizedVecType<TRank, TVector>.ISizedVisitor<IStatement>
    {
        public IStatement Visit<TElement>(VecType<TRank, TElement> t) where TElement : IScalarType<TElement>
            => new VectorComponentSetStatement<TRank, TElement, TComponent>(Target, Value);
    }

    public IShaderType LeftType => TVector.Instance.GetPtrType();
    public IShaderType RightType => TVector.Instance.ElementType;

    public IStatement CreateStatement(IExpression target, IExpression value)
        => TVector.Instance.Accept(new SizedVecStmtVisitor(target, value));

    public IStatementOperationValueInstruction ToValueInstruction(IValue l, IValue r)
    {
        throw new NotImplementedException();
    }
}
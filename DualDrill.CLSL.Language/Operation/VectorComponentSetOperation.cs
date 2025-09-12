using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Operation;

public interface IVectorComponentSetOperation
    : IBinaryStatementOperation
{
    IRank Range { get; }
    IScalarType ElementType { get; }
    IVecType VecType { get; }
    Swizzle.IComponent Component { get; }
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

    public IRank Range => TRank.Instance;

    public IScalarType ElementType => TVector.Instance.ElementType;

    public Swizzle.IComponent Component => TComponent.Instance;

    public IVecType VecType => TVector.Instance;

    public IStatement CreateStatement(IExpression target, IExpression value)
        => TVector.Instance.Accept(new SizedVecStmtVisitor(target, value));

    public TO EvaluateInstruction<TV, TR, TS, TO>(Instruction2<TV, TR> inst, TS semantic) where TS : IOperationSemantic<Instruction2<TV, TR>, TV, TR, TO>
        => semantic.VectorComponentSet(inst, this, inst.Operand0, inst.Operand1);
}
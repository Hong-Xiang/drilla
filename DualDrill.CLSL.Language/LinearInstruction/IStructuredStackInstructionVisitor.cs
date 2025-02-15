using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.LinearInstruction;

public interface IStructuredStackInstructionVisitor<TResult>
{
    TResult Visit(BrInstruction inst);
    TResult Visit(BrIfInstruction inst);
    TResult Visit(ReturnInstruction inst);
    TResult Visit(NopInstruction inst);
    TResult Visit<TLiteral>(ConstInstruction<TLiteral> inst) where TLiteral : ILiteral;
    TResult Visit(CallInstruction inst);
    TResult Visit<TTarget>(LoadSymbolValueInstruction<TTarget> inst)
        where TTarget : ILoadStoreTargetSymbol;
    TResult Visit<TTarget>(LoadSymbolAddressInstruction<TTarget> inst)
        where TTarget : ILoadStoreTargetSymbol;
    TResult Visit<TTarget>(StoreSymbolInstruction<TTarget> inst)
        where TTarget : ILoadStoreTargetSymbol;
    TResult Visit<TOperation>(BinaryOperationInstruction<TOperation> inst)
        where TOperation : ISingleton<TOperation>, IBinaryOperation<TOperation>;
    TResult Visit(LogicalNotInstruction inst);
    TResult Visit(DupInstruction inst);
    TResult Visit(DropInstruction inst);

    TResult VisitUnaryOperation<TOperation>(UnaryOperationInstruction<TOperation> inst)
        where TOperation : IUnaryOperation<TOperation>;

    TResult VisitVectorComponentGet<TRank, TVector, TComponent>()
        where TRank : IRank<TRank>
        where TVector : ISizedVecType<TRank, TVector>
        where TComponent : Swizzle.ISizedComponent<TRank, TComponent>;
    TResult VisitVectorComponentSet<TRank, TVector, TComponent>()
        where TRank : IRank<TRank>
        where TVector : ISizedVecType<TRank, TVector>
        where TComponent : Swizzle.ISizedComponent<TRank, TComponent>;


    TResult VisitVectorSwizzleGet<TPattern, TElement>()
        where TPattern : Swizzle.IPattern<TPattern>
        where TElement : IScalarType<TElement>;
    TResult VisitVectorSwizzleSet<TPattern, TElement>()
        where TPattern : Swizzle.IPattern<TPattern>
        where TElement : IScalarType<TElement>;
}

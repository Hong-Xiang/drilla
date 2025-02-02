using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.Common;

namespace DualDrill.CLSL.LinearInstruction;

public interface IStructuredStackInstructionVisitor<TResult>
{
    TResult Visit(BrInstruction inst);
    TResult Visit(BrIfInstruction inst);
    TResult Visit(ReturnInstruction inst);
    TResult Visit(NopInstruction inst);
    TResult Visit<TLiteral>(ConstInstruction<TLiteral> inst) where TLiteral : ILiteral;
    TResult Visit(CallInstruction inst);
    TResult Visit<TTarget>(LoadSymbolInstruction<TTarget> inst)
        where TTarget : IVariableIdentifierResolveResult;
    TResult Visit<TTarget>(LoadSymbolAddressInstruction<TTarget> inst)
        where TTarget : IVariableIdentifierResolveResult;
    TResult Visit<TTarget>(StoreSymbolInstruction<TTarget> inst)
        where TTarget : IVariableIdentifierResolveResult;
    TResult Visit<TOperation>(BinaryOperationInstruction<TOperation> inst)
        where TOperation : ISingleton<TOperation>, IBinaryOperation<TOperation>;
    TResult Visit(LogicalNotInstruction inst);

    TResult Visit<TOperation>(UnaryScalarInstruction<TOperation> inst)
        where TOperation : IUnaryScalarOperation<TOperation>;
}

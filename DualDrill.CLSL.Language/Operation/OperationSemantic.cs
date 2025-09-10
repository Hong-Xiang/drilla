using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation.Pointer;

namespace DualDrill.CLSL.Language.Operation;

public interface IOperationInstructionSemantic<in TX, in TV, in TR, out TO>
{
    TO Nop(TX ctx, NopOperation op);
    TO Load(TX ctx, LoadOperation op, TR result, TV ptr);
    TO Store(TX ctx, StoreOperation op, TV ptr, TV value);
    TO VectorSwizzleSet(TX ctx, IVectorSwizzleSetOperation op, TV ptr, TV value);
    TO Call(TX ctx, CallOperation op, TR result, FunctionDeclaration f, IReadOnlyList<TV> arguments);
    TO Literal<TLiteral>(TX ctx, LiteralOperation op, TR result, TLiteral value) where TLiteral : ILiteral;
    TO AddressOfChain(TX ctx, IAccessChainOperation op, TR result, TV target);
    TO AddressOfChain(TX ctx, IAccessChainOperation op, TR result, TV target, TV index);
    TO Operation1(TX ctx, IUnaryExpressionOperation op, TR result, TV e);
    TO Operation2(TX ctx, IBinaryExpressionOperation op, TR result, TV l, TV r);
    TO VectorCompositeConstruction(TX ctx, VectorCompositeConstructionOperation op, TR result, IReadOnlyList<TV> components);
}

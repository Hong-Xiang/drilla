using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation.Pointer;

namespace DualDrill.CLSL.Language.Operation;

public interface IOperationSemantic<in TX, in TV, in TR, out TO>
{
    TO Nop(TX ctx, NopOperation op);
    TO Load(TX ctx, LoadOperation op, TR result, TV ptr);
    TO Store(TX ctx, StoreOperation op, TV ptr, TV value);
    TO VectorSwizzleSet(TX ctx, IVectorSwizzleSetOperation op, TV ptr, TV value);
    TO VectorComponentSet(TX ctx, IVectorComponentSetOperation op, TV ptr, TV value);
    TO Call(TX ctx, CallOperation op, TR result, TV f, IReadOnlyList<TV> arguments);
    TO Literal(TX ctx, LiteralOperation op, TR result, ILiteral value);
    TO AddressOfChain(TX ctx, IAddressOfOperation op, TR result, TV target);
    TO AddressOfChain(TX ctx, IAddressOfOperation op, TR result, TV target, TV index);
    TO AccessChain(TX ctx, AccessChainOperation op, TR result, TV target, IReadOnlyList<TV> indices);
    TO Operation1(TX ctx, IUnaryExpressionOperation op, TR result, TV e);
    TO Operation2(TX ctx, IBinaryExpressionOperation op, TR result, TV l, TV r);
    TO VectorCompositeConstruction(TX ctx, VectorCompositeConstructionOperation op, TR result,
        IReadOnlyList<TV> components);
}
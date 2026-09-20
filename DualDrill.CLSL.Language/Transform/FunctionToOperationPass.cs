using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.ShaderAttribute.Metadata;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.Transform;

internal sealed class OperationFunctionNotMatchException : Exception
{
    public OperationFunctionNotMatchException(FunctionDeclaration function, IOperation operation)
        : base($"function {function} does not match operation {operation}")
    {
    }
}

public sealed class FunctionToOperationPass
    : IShaderModuleSimplePass
{
    public IDeclaration? VisitFunction(FunctionDeclaration decl) => decl;

    public RegionFunctionBody VisitFunctionBody(RegionFunctionBody body)
    {
        return new RegionFunctionBody(
            body.Declaration,
            body.Body.Select(
                l => l,
                rbody => rbody.MapInstruction(TransformInstruction)
            )
        );
    }

    public IDeclaration? VisitMember(MemberDeclaration decl) => decl;

    public IDeclaration? VisitParameter(ParameterDeclaration decl) => decl;

    public IDeclaration? VisitStructure(StructureDeclaration decl) => decl;

    public IDeclaration? VisitValue(ValueDeclaration decl) => decl;

    public IDeclaration? VisitVariable(VariableDeclaration decl) => decl;

    private IEnumerable<Instruction<IShaderValue, IShaderValue>> TransformInstruction(
        Instruction<IShaderValue, IShaderValue> inst) =>
        inst.Operation is CallOperation &&
        inst.OperandCount > 0 &&
        inst[0] is FunctionDeclaration
            ? inst.Evaluate(new InstructionTransformSemantic())
            : [inst];

    private sealed record class InstructionTransformSemantic
        : IOperationSemantic<Instruction<IShaderValue, IShaderValue>, IShaderValue, IShaderValue,
            IEnumerable<Instruction<IShaderValue, IShaderValue>>>
    {
        private IOperationSemantic<Unit, IShaderValue, IShaderValue, Instruction<IShaderValue, IShaderValue>> InstF
        {
            get;
        } = Instruction.Instruction.Factory;

        public IEnumerable<Instruction<IShaderValue, IShaderValue>> AccessChain(
            Instruction<IShaderValue, IShaderValue> ctx, AccessChainOperation op, IShaderValue result,
            IShaderValue target, IReadOnlyList<IShaderValue> indices)
            => [ctx];

        public IEnumerable<Instruction<IShaderValue, IShaderValue>> AddressOfChain(
            Instruction<IShaderValue, IShaderValue> ctx, IAddressOfOperation op, IShaderValue result,
            IShaderValue target) => [ctx];

        public IEnumerable<Instruction<IShaderValue, IShaderValue>> AddressOfChain(
            Instruction<IShaderValue, IShaderValue> ctx, IAddressOfOperation op, IShaderValue result,
            IShaderValue target, IShaderValue index) => [ctx];

        public IEnumerable<Instruction<IShaderValue, IShaderValue>> Call(Instruction<IShaderValue, IShaderValue> ctx,
            CallOperation op, IShaderValue result, IShaderValue fv, IReadOnlyList<IShaderValue> arguments)
        {
            if (fv is not FunctionDeclaration f)
                return [ctx];

            if (f.Attributes.OfType<IOperationMethodAttribute>().SingleOrDefault() is { } opAttr)
                switch (opAttr.Operation)
                {
                    case StructuredBufferLengthOperation length:
                        {
                            if (arguments is not [var buffer] ||
                                !buffer.Type.Equals(length.BufferPointerType) ||
                                !result.Type.Equals(ShaderType.U32))
                                throw new OperationFunctionNotMatchException(f, length);
                            return
                            [
                                WithPayload(
                                    InstF.StructuredBufferLength(default, length, result, buffer),
                                    ctx)
                            ];
                        }
                    case StructuredBufferLoadOperation load:
                        {
                            if (arguments is not [var buffer, var index] ||
                                !buffer.Type.Equals(load.BufferPointerType) ||
                                !index.Type.Equals(ShaderType.U32) ||
                                !result.Type.Equals(ShaderType.F32))
                                throw new OperationFunctionNotMatchException(f, load);
                            return
                            [
                                WithPayload(
                                    InstF.StructuredBufferLoad(default, load, result, buffer, index),
                                    ctx)
                            ];
                        }
                    case IBinaryExpressionOperation be:
                        {
                            var r = arguments[1];
                            var l = arguments[0];
                            if (!l.Type.Equals(be.LeftType) || !r.Type.Equals(be.RightType))
                                throw new OperationFunctionNotMatchException(f, be);
                            return [WithPayload(InstF.Operation2(default, be, result, l, r), ctx)];
                        }
                    case IBinaryStatementOperation bs:
                        {
                            var r = arguments[1];
                            var l = arguments[0];
                            if (!l.Type.Equals(bs.LeftType) || !r.Type.Equals(bs.RightType))
                            {
                                if (l.Type is IPtrType lp && bs.LeftType is IPtrType bp && lp.BaseType.Equals(bp.BaseType))
                                {
                                    // TODO: correct handling of address space equality
                                }
                                else
                                {
                                    throw new OperationFunctionNotMatchException(f, bs);
                                }
                            }

                            if (bs is IVectorComponentSetOperation vcs)
                            {
                                return
                                [
                                    WithPayload(InstF.VectorComponentSet(default, vcs, l, r), ctx)
                                ];
                            }

                            if (bs is IVectorSwizzleSetOperation vss)
                                return
                                [
                                    WithPayload(InstF.VectorSwizzleSet(default, vss, l, r), ctx)
                                ];

                            throw new NotSupportedException($"binary statement {bs.Name}");
                        }
                    case IUnaryExpressionOperation ue:
                        {
                            var s = arguments[0];
                            if (!s.Type.Equals(ue.SourceType))
                            {
                                if (s.Type is IPtrType ps && ue.SourceType is IPtrType pu &&
                                    ps.BaseType.Equals(pu.BaseType))
                                {
                                    // TODO: modify operation to support precise control on address space 
                                }
                                else
                                {
                                    throw new OperationFunctionNotMatchException(f, ue);
                                }
                            }

                            return
                            [
                                WithPayload(InstF.Operation1(default, ue, result, s), ctx)
                            ];
                        }
                }

            if (f.Attributes.OfType<ZeroConstructorMethodAttribute>().Any() && f.ReturnType is IVecType vt)
            {
                return [
                    WithPayload(InstF.ZeroConstructorOperation(default, new ZeroConstructorOperation(vt), result), ctx)
                ];
            }

            if (f.Attributes.OfType<VectorCompositeConstructorMethodAttribute>().SingleOrDefault() is { } vcc)
            {
                var op_ = (VectorCompositeConstructionOperation)vcc.GetOperation(f.Return.Type,
                    f.Parameters.Select(p => p.Type));
                return
                [
                    WithPayload(InstF.VectorCompositeConstruction(default, op_, result, arguments), ctx)
                ];
            }

            return [ctx];
        }

        private static Instruction<IShaderValue, IShaderValue> WithPayload(
            Instruction<IShaderValue, IShaderValue> replacement,
            Instruction<IShaderValue, IShaderValue> original) =>
            replacement with { Payload = original.Payload };

        public IEnumerable<Instruction<IShaderValue, IShaderValue>> Literal(
            Instruction<IShaderValue, IShaderValue> ctx, LiteralOperation op, IShaderValue result, IShaderValue value) =>
            [ctx];

        public IEnumerable<Instruction<IShaderValue, IShaderValue>> Load(Instruction<IShaderValue, IShaderValue> ctx,
            LoadOperation op, IShaderValue result, IShaderValue ptr) => [ctx];

        public IEnumerable<Instruction<IShaderValue, IShaderValue>> Nop(Instruction<IShaderValue, IShaderValue> ctx,
            NopOperation op) => [ctx];


        public IEnumerable<Instruction<IShaderValue, IShaderValue>> Operation1(
            Instruction<IShaderValue, IShaderValue> ctx, IUnaryExpressionOperation op, IShaderValue result,
            IShaderValue e) => [ctx];

        public IEnumerable<Instruction<IShaderValue, IShaderValue>> Operation2(
            Instruction<IShaderValue, IShaderValue> ctx, IBinaryExpressionOperation op, IShaderValue result,
            IShaderValue l, IShaderValue r) => [ctx];

        public IEnumerable<Instruction<IShaderValue, IShaderValue>> StructuredBufferLength(
            Instruction<IShaderValue, IShaderValue> ctx,
            StructuredBufferLengthOperation op,
            IShaderValue result,
            IShaderValue buffer) =>
            [ctx];

        public IEnumerable<Instruction<IShaderValue, IShaderValue>> StructuredBufferLoad(
            Instruction<IShaderValue, IShaderValue> ctx,
            StructuredBufferLoadOperation op,
            IShaderValue result,
            IShaderValue buffer,
            IShaderValue index) =>
            [ctx];


        public IEnumerable<Instruction<IShaderValue, IShaderValue>> Store(Instruction<IShaderValue, IShaderValue> ctx,
            StoreOperation op, IShaderValue ptr, IShaderValue value) => [ctx];

        public IEnumerable<Instruction<IShaderValue, IShaderValue>> VectorComponentSet(
            Instruction<IShaderValue, IShaderValue> ctx, IVectorComponentSetOperation op, IShaderValue ptr,
            IShaderValue value) => [ctx];

        public IEnumerable<Instruction<IShaderValue, IShaderValue>> VectorCompositeConstruction(
            Instruction<IShaderValue, IShaderValue> ctx, VectorCompositeConstructionOperation op, IShaderValue result,
            IReadOnlyList<IShaderValue> components) => [ctx];


        public IEnumerable<Instruction<IShaderValue, IShaderValue>> VectorSwizzleSet(
            Instruction<IShaderValue, IShaderValue> ctx, IVectorSwizzleSetOperation op, IShaderValue ptr,
            IShaderValue value) => [ctx];

        public IEnumerable<Instruction<IShaderValue, IShaderValue>> ZeroConstructorOperation(Instruction<IShaderValue, IShaderValue> ctx, ZeroConstructorOperation op, IShaderValue result)
            => [ctx];
    }
}
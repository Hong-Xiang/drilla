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
        Instruction<IShaderValue, IShaderValue> inst)
    {
        if (inst.Operation is not CallOperation call)
            return [inst];
        if (inst.Operand0 is not FunctionDeclaration function)
        {
            var resourceOperation = ResourceOperations()
                .FirstOrDefault(operation =>
                    operation.Function.Type is FunctionType type &&
                    type.Equals(call.CalleeType));
            if (resourceOperation is not null)
                throw new OperationFunctionNotMatchException(resourceOperation.Function, resourceOperation);
            return [inst];
        }

        var operation = function.Attributes.OfType<IOperationMethodAttribute>().SingleOrDefault()?.Operation;
        if (operation is IReadOnlyStructuredBufferLengthOperation or IReadOnlyStructuredBufferLoadOperation &&
            !ReadOnlyStructuredBufferFamily.IsCanonicalLength(operation) &&
            !ReadOnlyStructuredBufferFamily.IsCanonicalLoad(operation))
            throw new OperationFunctionNotMatchException(function, operation);
        if (operation is not null && IsResourceOperation(operation))
        {
            var expectedOperands = operation.Function.Parameters.Length + 1;
            var expectsResult = operation is not ReadWriteStructuredBufferStoreOperation;
            if (!HasPhysicalOperandShape(inst, expectedOperands) ||
                expectsResult != (inst.Result is not null))
                throw new OperationFunctionNotMatchException(function, operation);
        }

        return inst.Evaluate(new InstructionTransformSemantic());
    }

    private static bool IsResourceOperation(IOperation operation) =>
        ReadOnlyStructuredBufferFamily.IsCanonicalLength(operation) ||
        ReadOnlyStructuredBufferFamily.IsCanonicalLoad(operation) ||
        operation is
            ReadWriteStructuredBufferLengthOperation or
            ReadWriteStructuredBufferLoadOperation or
            ReadWriteStructuredBufferStoreOperation or
            TextureSampleLevelOperation;

    private static IEnumerable<IOperation> ResourceOperations()
    {
        foreach (var operation in ReadOnlyStructuredBufferFamily.CanonicalOperations)
            yield return operation;
        yield return ReadWriteStructuredBufferLengthOperation.Instance;
        yield return ReadWriteStructuredBufferLoadOperation.Instance;
        yield return ReadWriteStructuredBufferStoreOperation.Instance;
        yield return TextureSampleLevelOperation.Instance;
    }

    private static bool HasPhysicalOperandShape(
        Instruction<IShaderValue, IShaderValue> instruction,
        int expectedCount) =>
        instruction.OperandCount == expectedCount &&
        instruction.Operand0 is not null &&
        (expectedCount == 1
            ? instruction.Operand1 is null
            : instruction.Operand1 is not null) &&
        !instruction.RestOperands.IsDefault &&
        instruction.RestOperands.Length == Math.Max(0, expectedCount - 2) &&
        instruction.RestOperands.All(static operand => operand is not null);

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
                    case IReadOnlyStructuredBufferLengthOperation length:
                        {
                            if (!ReadOnlyStructuredBufferFamily.IsCanonicalLength(length) ||
                                !IsExactResourceFunction(op, f, length) ||
                                arguments is not [var buffer] ||
                                !buffer.Type.Equals(length.BufferPointerType) ||
                                ctx.Result is not { } lengthResult ||
                                !lengthResult.Type.Equals(ShaderType.U32))
                                throw new OperationFunctionNotMatchException(f, length);
                            return
                            [
                                WithPayload(
                                    InstF.StructuredBufferLength(default, length, lengthResult, buffer),
                                    ctx)
                            ];
                        }
                    case IReadOnlyStructuredBufferLoadOperation load:
                        {
                            if (!ReadOnlyStructuredBufferFamily.IsCanonicalLoad(load) ||
                                !IsExactResourceFunction(op, f, load) ||
                                arguments is not [var buffer, var index] ||
                                !buffer.Type.Equals(load.BufferPointerType) ||
                                !index.Type.Equals(ShaderType.U32) ||
                                ctx.Result is not { } loadResult ||
                                !loadResult.Type.Equals(load.ElementType))
                                throw new OperationFunctionNotMatchException(f, load);
                            return
                            [
                                WithPayload(
                                    InstF.StructuredBufferLoad(default, load, loadResult, buffer, index),
                                    ctx)
                            ];
                        }
                    case ReadWriteStructuredBufferLengthOperation length:
                        {
                            if (!IsExactResourceFunction(op, f, length) ||
                                arguments is not [var buffer] ||
                                !buffer.Type.Equals(length.BufferPointerType) ||
                                ctx.Result is not { } lengthResult ||
                                !lengthResult.Type.Equals(ShaderType.U32))
                                throw new OperationFunctionNotMatchException(f, length);
                            return
                            [
                                WithPayload(
                                    InstF.ReadWriteStructuredBufferLength(default, length, lengthResult, buffer),
                                    ctx)
                            ];
                        }
                    case ReadWriteStructuredBufferLoadOperation load:
                        {
                            if (!IsExactResourceFunction(op, f, load) ||
                                arguments is not [var buffer, var index] ||
                                !buffer.Type.Equals(load.BufferPointerType) ||
                                !index.Type.Equals(ShaderType.U32) ||
                                ctx.Result is not { } loadResult ||
                                !loadResult.Type.Equals(ShaderType.F32))
                                throw new OperationFunctionNotMatchException(f, load);
                            return
                            [
                                WithPayload(
                                    InstF.ReadWriteStructuredBufferLoad(default, load, loadResult, buffer, index),
                                    ctx)
                            ];
                        }
                    case ReadWriteStructuredBufferStoreOperation store:
                        {
                            if (!IsExactResourceFunction(op, f, store) ||
                                arguments is not [var buffer, var index, var value] ||
                                !buffer.Type.Equals(store.BufferPointerType) ||
                                !index.Type.Equals(ShaderType.U32) ||
                                !value.Type.Equals(ShaderType.F32) ||
                                ctx.Result is not null)
                                throw new OperationFunctionNotMatchException(f, store);
                            return
                            [
                                WithPayload(
                                    InstF.ReadWriteStructuredBufferStore(default, store, buffer, index, value),
                                    ctx)
                            ];
                        }
                    case TextureSampleLevelOperation sample:
                        {
                            if (!IsExactResourceFunction(op, f, sample) ||
                                arguments is not [var texture, var sampler, var uv, var lod] ||
                                !texture.Type.Equals(sample.TexturePointerType) ||
                                !sampler.Type.Equals(sample.SamplerPointerType) ||
                                !uv.Type.Equals(ShaderType.Vec2F32) ||
                                !lod.Type.Equals(ShaderType.F32) ||
                                ctx.Result is not { } sampleResult ||
                                !sampleResult.Type.Equals(ShaderType.Vec4F32))
                                throw new OperationFunctionNotMatchException(f, sample);
                            return
                            [
                                WithPayload(
                                    InstF.TextureSampleLevel(
                                        default,
                                        sample,
                                        sampleResult,
                                        texture,
                                        sampler,
                                        uv,
                                        lod),
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

        private static bool IsExactResourceFunction(
            CallOperation call,
            FunctionDeclaration actual,
            IOperation operation) =>
            ReferenceEquals(actual, operation.Function) &&
            actual.Type is FunctionType declared &&
            operation.Function.Type is FunctionType expected &&
            declared.Equals(expected) &&
            call.CalleeType.Equals(declared);

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
            IReadOnlyStructuredBufferLengthOperation op,
            IShaderValue result,
            IShaderValue buffer) =>
            [ctx];

        public IEnumerable<Instruction<IShaderValue, IShaderValue>> StructuredBufferLoad(
            Instruction<IShaderValue, IShaderValue> ctx,
            IReadOnlyStructuredBufferLoadOperation op,
            IShaderValue result,
            IShaderValue buffer,
            IShaderValue index) =>
            [ctx];

        public IEnumerable<Instruction<IShaderValue, IShaderValue>> ReadWriteStructuredBufferLength(
            Instruction<IShaderValue, IShaderValue> ctx,
            ReadWriteStructuredBufferLengthOperation op,
            IShaderValue result,
            IShaderValue buffer) =>
            [ctx];

        public IEnumerable<Instruction<IShaderValue, IShaderValue>> ReadWriteStructuredBufferLoad(
            Instruction<IShaderValue, IShaderValue> ctx,
            ReadWriteStructuredBufferLoadOperation op,
            IShaderValue result,
            IShaderValue buffer,
            IShaderValue index) =>
            [ctx];

        public IEnumerable<Instruction<IShaderValue, IShaderValue>> ReadWriteStructuredBufferStore(
            Instruction<IShaderValue, IShaderValue> ctx,
            ReadWriteStructuredBufferStoreOperation op,
            IShaderValue buffer,
            IShaderValue index,
            IShaderValue value) =>
            [ctx];

        public IEnumerable<Instruction<IShaderValue, IShaderValue>> TextureSampleLevel(
            Instruction<IShaderValue, IShaderValue> ctx,
            TextureSampleLevelOperation op,
            IShaderValue result,
            IShaderValue texture,
            IShaderValue sampler,
            IShaderValue uv,
            IShaderValue lod) =>
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
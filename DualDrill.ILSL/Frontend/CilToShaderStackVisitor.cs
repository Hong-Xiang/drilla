using System.Collections.Immutable;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Frontend;

internal sealed class CilToShaderStackVisitor : ICilInstructionVisitor<Unit>
{
    private readonly List<Annotated<ShaderStackInstruction, ShaderStackTransition>> instructions = [];
    private readonly List<IShaderType> stack;
    private readonly PointerOperationFactory pointer = new();
    private CilInstructionInfo current;
    private int ordinal;

    public CilToShaderStackVisitor(
        CilMethodEnvironment environment,
        FunctionDeclaration function,
        CilControlFlow control,
        ImmutableArray<IShaderType> inputStack)
    {
        Environment = environment;
        Function = function;
        Control = control;
        stack = [.. inputStack];
    }

    public CilMethodEnvironment Environment { get; }
    public FunctionDeclaration Function { get; }
    public CilControlFlow Control { get; }
    public ImmutableArray<IShaderType> Stack => [.. stack];
    public IReadOnlyList<Annotated<ShaderStackInstruction, ShaderStackTransition>> Instructions => instructions;
    public Annotated<ITerminator<Label, ShaderStackOperand>, ShaderStackTransition>? Terminator { get; private set; }

    public void Lower(CilInstructionInfo instruction, ISymbolTableView symbols)
    {
        current = instruction;
        ordinal = 0;
        instruction.Evaluate(this, Environment.IsStatic, symbols);
    }

    public Annotated<ITerminator<Label, ShaderStackOperand>, ShaderStackTransition> SyntheticTerminator(
        CilControlFlow control,
        CilInstructionInfo anchor)
    {
        if (Terminator is not null)
            throw new InvalidOperationException("A native terminator was already emitted.");
        return control switch
        {
            CilControlFlow.FallThrough { Target: var target } =>
                AnnotateTerminator(
                    TerminatorFactory.Br(target),
                    ShaderStackProvenance.SyntheticFallThrough(anchor)),
            CilControlFlow.EndOfCode =>
                throw new NotSupportedException(
                    $"Method {Environment.Method} reaches the end of CIL without an explicit return."),
            _ => throw new ValidationException(
                $"Native CIL control at IL_{anchor.ByteOffset:X4} did not produce a terminator.",
                Environment.Method)
        };
    }

    public Unit VisitLiteral<TLiteral>(CilInstructionInfo info, TLiteral literal) where TLiteral : ILiteral
    {
        Emit(new LiteralOperation(), literal.Type, [Immediate(ShaderValue.Literal(literal))], 0);
        return default;
    }

    public Unit VisitPop(CilInstructionInfo inst)
    {
        Emit(new ShaderStackInstruction.Drop());
        return default;
    }

    public Unit VisitReturn(CilInstructionInfo info)
    {
        RequireNativeControl<CilControlFlow.Return>(info);
        if (Function.Return.Type is UnitType)
        {
            if (stack.Count != 0)
                throw Invalid("Void return requires an empty evaluation stack.");
            SetTerminator(TerminatorFactory.ReturnVoid());
            return default;
        }

        if (stack.Count != 1)
            throw Invalid("Expression return requires exactly one evaluation-stack value.");
        ConvertTopForDeclaration(Function.Return.Type);
        SetTerminator(TerminatorFactory.ReturnExpr(Depth(0)));
        return default;
    }

    public Unit VisitNop(CilInstructionInfo inst)
    {
        Emit(NopOperation.Instance, null, [], 0);
        return default;
    }

    public Unit VisitBreak(CilInstructionInfo inst) => throw new NotSupportedException();

    public Unit VisitLoadArgument(CilInstructionInfo inst, ParameterDeclaration parameter)
    {
        Load(parameter.Value);
        return default;
    }

    public Unit VisitBinaryArithmetic<TOp>(CilInstructionInfo inst, bool isUn = false, bool isChecked = false)
        where TOp : BinaryArithmetic.IOp<TOp>
    {
        if (isChecked)
            throw new NotSupportedException($"Checked arithmetic is not supported at IL_{inst.ByteOffset:X4}.");
        var (left, right) = TopBinaryTypes();
        if (!left.Equals(right))
            throw Invalid($"Binary operation type mismatch: {left.Name} != {right.Name}.");

        var unsignedInteger = isUn && left is IntType<N32> or IntType<N64>;
        IBinaryExpressionOperation operation;
        if (isUn && left is IntType<N32>)
            operation = NumericBinaryArithmeticOperation<UIntType<N32>, TOp>.Instance;
        else if (isUn && left is IntType<N64>)
            operation = NumericBinaryArithmeticOperation<UIntType<N64>, TOp>.Instance;
        else
            operation = left switch
            {
                IntType<N32> => NumericBinaryArithmeticOperation<IntType<N32>, TOp>.Instance,
                IntType<N64> => NumericBinaryArithmeticOperation<IntType<N64>, TOp>.Instance,
                FloatType<N32> => NumericBinaryArithmeticOperation<FloatType<N32>, TOp>.Instance,
                FloatType<N64> => NumericBinaryArithmeticOperation<FloatType<N64>, TOp>.Instance,
                _ => throw Invalid($"Operation {TOp.Instance.Name} does not support {left.Name}.")
            };
        if (unsignedInteger)
            EmitUnsignedBinary(operation);
        else
            EmitBinary(operation);
        NormalizeTop(operation.ResultType);
        return default;
    }

    public Unit VisitLoadArgumentAddress(CilInstructionInfo inst, ParameterDeclaration parameter)
    {
        PushAlias(parameter.Value);
        return default;
    }

    public Unit VisitStoreArgument(CilInstructionInfo inst, ParameterDeclaration parameter)
    {
        Store(parameter.Value);
        return default;
    }

    public Unit VisitLdThis(CilInstructionInfo inst) => throw new NotImplementedException();
    public Unit VisitStThis(CilInstructionInfo inst) => throw new NotImplementedException();

    public Unit VisitLoadLocal(CilInstructionInfo inst, VariableDeclaration variable)
    {
        Load(variable.Value);
        return default;
    }

    public Unit VisitLoadLocalAddress(CilInstructionInfo inst, VariableDeclaration variable)
    {
        PushAlias(variable.Value);
        return default;
    }

    public Unit VisitInitObject(CilInstructionInfo inst, Type type, IShaderType mappedType)
    {
        if (TopType() is not IPtrType pointerType ||
            pointerType.AddressSpace.Kind != AddressSpaceKind.Function ||
            !pointerType.BaseType.Equals(mappedType))
            throw Invalid($"initobj {type} requires a matching function-local pointer.");
        EmitZero(mappedType);
        Emit(new StoreOperation(), null, [Depth(1), Depth(0)], 2);
        return default;
    }

    private void EmitZero(IShaderType type)
    {
        switch (type)
        {
            case BoolType:
                ZeroLiteral(new BoolLiteral(false));
                return;
            case IntType<N32>:
                ZeroLiteral(new I32Literal(0));
                return;
            case UIntType<N32>:
                ZeroLiteral(new U32Literal(0u));
                return;
            case FloatType<N32>:
                ZeroLiteral(new F32Literal(0.0f));
                return;
            case IVecType vector:
                for (var index = 0; index < vector.Size.Value; index++)
                    EmitZero(vector.ElementType);
                var components = Enumerable.Range(0, vector.Size.Value).Select(index => (IShaderType)vector.ElementType);
                var vectorOperation = VectorCompositeConstructionOperation.Get(vector, components);
                Emit(vectorOperation, type,
                    Enumerable.Range(0, vector.Size.Value).Reverse().Select(Depth), vector.Size.Value);
                return;
            case StructureType structure:
                foreach (var member in structure.Declaration.Members)
                    EmitZero(member.Type);
                var count = structure.Declaration.Members.Length;
                Emit(new StructureCompositeConstructionOperation(structure), type,
                    Enumerable.Range(0, count).Reverse().Select(Depth), count);
                return;
            default:
                throw Invalid($"initobj zero construction of {type.Name} is not supported.");
        }
    }

    private void ZeroLiteral(ILiteral literal) =>
        Emit(new LiteralOperation(), literal.Type, [Immediate(ShaderValue.Literal(literal))], 0);

    public Unit VisitStoreLocal(CilInstructionInfo inst, VariableDeclaration variable)
    {
        Store(variable.Value);
        return default;
    }

    public Unit VisitLoadField(CilInstructionInfo inst, MemberDeclaration member)
    {
        if (TopType() is StructureType structure)
        {
            Emit(new StructureMemberGetOperation(structure, member), member.Type, [Depth(0)], 1);
            NormalizeTop(member.Type);
            return default;
        }
        AddressOfMember(member, 1);
        LoadFromTop();
        return default;
    }

    public Unit VisitLoadFieldAddress(CilInstructionInfo inst, MemberDeclaration member)
    {
        AddressOfMember(member, 1);
        return default;
    }

    public Unit VisitStoreField(CilInstructionInfo inst, MemberDeclaration member)
    {
        if (stack.Count < 2 || stack[^2] is not IPtrType owner)
            throw Invalid($"Cannot store field {member.Name} without a structure pointer below the value.");
        var pointerType = member.Type.GetPtrType(owner.AddressSpace);
        Emit(pointer.Member(member), pointerType, [Depth(1)], 0);
        var converted = ConvertAtDepthForDeclaration(member.Type, 1);
        var pointerDepth = converted ? 1 : 0;
        var valueDepth = converted ? 0 : 1;
        Emit(new StoreOperation(), null, [Depth(pointerDepth), Depth(valueDepth)], converted ? 4 : 3);
        return default;
    }

    public Unit VisitLoadStaticField(CilInstructionInfo inst, VariableDeclaration variable)
    {
        Load(variable.Value);
        return default;
    }

    public Unit VisitLoadStaticFieldAddress(CilInstructionInfo inst, VariableDeclaration variable)
    {
        PushAlias(variable.Value);
        return default;
    }

    public Unit VisitLoadNull(CilInstructionInfo info) => throw new NotImplementedException();

    public Unit VisitCall(CilInstructionInfo info, FunctionDeclaration function)
    {
        Call(function, function.Return.Type is not UnitType);
        return default;
    }

    public Unit VisitNewObject(CilInstructionInfo info, FunctionDeclaration function)
    {
        Call(function, true);
        return default;
    }

    public Unit VisitBranch(CilInstructionInfo inst, int jumpOffset)
    {
        var control = RequireNativeControl<CilControlFlow.Branch>(inst);
        SetTerminator(TerminatorFactory.Br(control.Target));
        return default;
    }

    public Unit VisitBranchIf(CilInstructionInfo inst, int jumpOffset, bool value)
    {
        if (!TopType().Equals(ShaderType.I32))
            throw Invalid($"Conditional branch expects i32, got {TopType().Name}.");
        Emit(ScalarConversionOperation<IntType<N32>, BoolType>.Instance, ShaderType.Bool, [Depth(0)], 1);
        var control = RequireNativeControl<CilControlFlow.ConditionalBranch>(inst);
        SetTerminator(value
            ? TerminatorFactory.BrIf(Depth(0), control.BranchTarget, control.FallThroughTarget)
            : TerminatorFactory.BrIf(Depth(0), control.FallThroughTarget, control.BranchTarget));
        return default;
    }

    public Unit VisitBranchIf<TOp>(CilInstructionInfo inst, int jumpOffset, bool isUn = false)
        where TOp : BinaryRelational.IOp<TOp>
    {
        EmitRelation<TOp>(isUn);
        var control = RequireNativeControl<CilControlFlow.ConditionalBranch>(inst);
        SetTerminator(TerminatorFactory.BrIf(Depth(0), control.BranchTarget, control.FallThroughTarget));
        return default;
    }

    public Unit VisitSwitch(CilInstructionInfo inst)
    {
        if (!TopType().Equals(ShaderType.I32))
            throw Invalid($"Switch expects i32, got {TopType().Name}.");
        var control = RequireNativeControl<CilControlFlow.Switch>(inst);
        SetTerminator(TerminatorFactory.Switch(Depth(0), control.CaseTargets, control.DefaultTarget));
        return default;
    }

    public Unit VisitBinaryLogical<TOp>(CilInstructionInfo inst) where TOp : BinaryLogical.IOp<TOp>
    {
        var (left, right) = TopBinaryTypes();
        if (!left.Equals(ShaderType.I32) || !right.Equals(ShaderType.I32) ||
            TOp.Instance is not BinaryLogical.IWithBitwiseOp bitwise)
            throw Invalid($"Logical operation {TOp.Instance.Name} requires i32 operands.");
        EmitBinary(bitwise.BitwiseOp.GetNumericBinaryOperation(IntType<N32>.Instance));
        return default;
    }

    public Unit VisitBinaryRelation<TOp>(CilInstructionInfo inst, bool isUn = false, bool isChecked = false)
        where TOp : BinaryRelational.IOp<TOp>
    {
        if (isChecked)
            throw new NotSupportedException($"Checked comparison is not supported at IL_{inst.ByteOffset:X4}.");
        EmitRelation<TOp>(isUn);
        NormalizeTop(ShaderType.Bool);
        return default;
    }

    public Unit VisitConversion<TTarget>(CilInstructionInfo inst) where TTarget : IScalarType<TTarget>
    {
        if (TopType() is not IScalarType source)
            throw Invalid($"Conversion source {TopType().Name} is not scalar.");
        var operation = source.GetConversionToOperation<TTarget>();
        Emit(operation, operation.ResultType, [Depth(0)], 1);
        NormalizeTop(operation.ResultType);
        return default;
    }

    public Unit VisitLogicalNot(CilInstructionInfo inst) => throw new NotImplementedException();

    public Unit VisitUnaryArithmetic<TOp>(CilInstructionInfo inst) where TOp : UnaryArithmetic.IOp<TOp>
    {
        var type = TopType();
        if (type is not (IntType<N32> or IntType<N64> or FloatType<N32> or FloatType<N64>))
            throw Invalid($"Unary arithmetic {TOp.Instance.Name} requires i32, i64, f32, or f64.");
        var operation = ((INumericType)type).UnaryArithmeticOperation<TOp>();
        Emit(operation, operation.ResultType, [Depth(0)], 1);
        NormalizeTop(operation.ResultType);
        return default;
    }

    public Unit VisitLoadIndirect<TShaderType>(CilInstructionInfo inst) where TShaderType : IShaderType =>
        throw new NotImplementedException();
    public Unit VisitStoreIndirect<TShaderType>(CilInstructionInfo inst) where TShaderType : IShaderType =>
        throw new NotImplementedException();
    public Unit VisitLoadIndirectNativeInt(CilInstructionInfo inst) => throw new NotImplementedException();
    public Unit VisitLoadIndirectRef(CilInstructionInfo inst) => throw new NotImplementedException();
    public Unit VisitStoreIndirectRef(CilInstructionInfo inst) => throw new NotImplementedException();
    public Unit VisitDup(CilInstructionInfo inst)
    {
        Emit(new ShaderStackInstruction.Duplicate());
        return default;
    }

    private void EmitRelation<TOp>(bool isUn)
        where TOp : BinaryRelational.IOp<TOp>
    {
        var (left, right) = TopBinaryTypes();
        if (!left.Equals(right))
            throw Invalid($"Relational operation type mismatch: {left.Name} != {right.Name}.");

        if (isUn && TOp.Instance is not BinaryRelational.Ne && left is IntType<N32>)
        {
            EmitUnsignedBinary(NumericBinaryRelationalOperation<UIntType<N32>, TOp>.Instance);
            return;
        }
        if (isUn && TOp.Instance is not BinaryRelational.Ne && left is IntType<N64>)
        {
            EmitUnsignedBinary(NumericBinaryRelationalOperation<UIntType<N64>, TOp>.Instance);
            return;
        }
        if (isUn && left is FloatType<N32>)
        {
            EmitUnorderedFloatRelation<TOp, FloatType<N32>>();
            return;
        }
        if (isUn && left is FloatType<N64>)
        {
            EmitUnorderedFloatRelation<TOp, FloatType<N64>>();
            return;
        }

        EmitBinary(left switch
        {
            IntType<N32> => NumericBinaryRelationalOperation<IntType<N32>, TOp>.Instance,
            IntType<N64> => NumericBinaryRelationalOperation<IntType<N64>, TOp>.Instance,
            FloatType<N32> => NumericBinaryRelationalOperation<FloatType<N32>, TOp>.Instance,
            FloatType<N64> => NumericBinaryRelationalOperation<FloatType<N64>, TOp>.Instance,
            _ => throw Invalid($"Relational operation {TOp.Instance.Name} does not support {left.Name}.")
        });
    }

    private void EmitUnorderedFloatRelation<TOp, TFloat>()
        where TOp : BinaryRelational.IOp<TOp>
        where TFloat : INumericType<TFloat>
    {
        if (TOp.Instance is BinaryRelational.Ne)
        {
            EmitBinary(NumericBinaryRelationalOperation<TFloat, BinaryRelational.Ne>.Instance);
            return;
        }

        IBinaryExpressionOperation inverse = TOp.Instance switch
        {
            BinaryRelational.Ge => NumericBinaryRelationalOperation<TFloat, BinaryRelational.Lt>.Instance,
            BinaryRelational.Gt => NumericBinaryRelationalOperation<TFloat, BinaryRelational.Le>.Instance,
            BinaryRelational.Le => NumericBinaryRelationalOperation<TFloat, BinaryRelational.Gt>.Instance,
            BinaryRelational.Lt => NumericBinaryRelationalOperation<TFloat, BinaryRelational.Ge>.Instance,
            _ => throw Invalid($"Unsupported unordered floating relation {TOp.Instance.Name}.")
        };
        EmitBinary(inverse);
        Emit(LogicalNotOperation.Instance, ShaderType.Bool, [Depth(0)], 1);
    }

    private void EmitUnsignedBinary(IBinaryExpressionOperation operation)
    {
        var unsigned = operation.LeftType;
        var signed = TopType();
        var leftConversion = Conversion(signed, unsigned);
        Emit(leftConversion, unsigned, [Depth(1)], 0);
        Emit(leftConversion, unsigned, [Depth(1)], 0);
        Emit(operation, operation.ResultType, [Depth(1), Depth(0)], 4);
    }

    private void EmitBinary(IBinaryExpressionOperation operation)
    {
        Emit(operation, operation.ResultType, [Depth(1), Depth(0)], 2);
    }

    private void AddressOfMember(MemberDeclaration member, int popCount)
    {
        if (TopType() is not IPtrType owner || owner.BaseType is not StructureType)
            throw Invalid($"Cannot address field {member.Name} from {TopType().Name}.");
        Emit(pointer.Member(member), member.Type.GetPtrType(owner.AddressSpace), [Depth(0)], popCount);
    }

    private void Load(IShaderValue pointerValue)
    {
        if (pointerValue.Type is not IPtrType pointerType)
            throw Invalid($"Cannot load from non-pointer {pointerValue.Type.Name}.");
        Emit(new LoadOperation(), pointerType.BaseType, [Immediate(pointerValue)], 0);
        NormalizeTop(pointerType.BaseType);
    }

    private void LoadFromTop()
    {
        if (TopType() is not IPtrType pointerType)
            throw Invalid($"Cannot load from non-pointer {TopType().Name}.");
        Emit(new LoadOperation(), pointerType.BaseType, [Depth(0)], 1);
        NormalizeTop(pointerType.BaseType);
    }

    private void Store(IShaderValue pointerValue)
    {
        if (pointerValue.Type is not IPtrType pointerType)
            throw Invalid($"Cannot store to non-pointer {pointerValue.Type.Name}.");
        ConvertTopForDeclaration(pointerType.BaseType);
        Emit(new StoreOperation(), null, [Immediate(pointerValue), Depth(0)], 1);
    }

    private void Call(FunctionDeclaration function, bool hasReturnValue)
    {
        if (function.Attributes.OfType<IOperationMethodAttribute>().SingleOrDefault() is { } attribute)
        {
            EmitIntrinsic(attribute.Operation);
            return;
        }

        var parameterCount = function.Parameters.Length;
        if (stack.Count < parameterCount)
            throw Invalid($"Call {function.Name} does not have enough stack arguments.");
        var firstArgument = stack.Count - parameterCount;
        var argumentPositions = new int[parameterCount];
        var conversionCount = 0;
        foreach (var (index, parameter) in function.Parameters.Index())
        {
            var sourcePosition = firstArgument + index;
            var sourceType = stack[sourcePosition];
            if (parameter.Type is BoolType && sourceType is IntType<N32>)
            {
                Emit(
                    ScalarConversionOperation<IntType<N32>, BoolType>.Instance,
                    ShaderType.Bool,
                    [Depth(stack.Count - 1 - sourcePosition)],
                    0);
                argumentPositions[index] = stack.Count - 1;
                conversionCount++;
            }
            else
            {
                if (!sourceType.Equals(parameter.Type))
                    throw Invalid(
                        $"parameter {parameter} not match: stack {sourceType.Name} declaration {parameter.Type.Name}");
                argumentPositions[index] = sourcePosition;
            }
        }

        var operands = new List<ShaderStackOperand> { Immediate(function) };
        operands.AddRange(argumentPositions.Select(position => Depth(stack.Count - 1 - position)));
        Emit(
            new CallOperation((FunctionType)function.Type),
            function.Return.Type,
            operands,
            parameterCount + conversionCount);
        if (hasReturnValue)
            NormalizeTop(function.Return.Type);
    }

    private void EmitIntrinsic(IOperation operation)
    {
        switch (operation)
        {
            case StructuredBufferLengthOperation length:
                if (!TopType().Equals(length.BufferPointerType))
                    throw Invalid($"{length.Name} operation stack: {TopType().Name}.");
                Emit(length, ShaderType.U32, [Depth(0)], 1);
                NormalizeTop(ShaderType.U32);
                return;
            case StructuredBufferLoadOperation load:
                if (stack.Count < 2 || !TypeAtDepth(1).Equals(load.BufferPointerType))
                    throw Invalid($"{load.Name} requires an exact storage-buffer receiver.");
                ConvertTopForDeclaration(ShaderType.U32);
                if (!TypeAtDepth(0).Equals(ShaderType.U32))
                    throw Invalid($"{load.Name} index must be u32.");
                Emit(load, ShaderType.F32, [Depth(1), Depth(0)], 2);
                return;
            case ReadWriteStructuredBufferLengthOperation rwLength:
                if (!TopType().Equals(rwLength.BufferPointerType))
                    throw Invalid($"{rwLength.Name} operation stack: {TopType().Name}.");
                Emit(rwLength, ShaderType.U32, [Depth(0)], 1);
                NormalizeTop(ShaderType.U32);
                return;
            case ReadWriteStructuredBufferLoadOperation rwLoad:
                if (stack.Count < 2 || !TypeAtDepth(1).Equals(rwLoad.BufferPointerType))
                    throw Invalid($"{rwLoad.Name} requires an exact storage-buffer receiver.");
                ConvertTopForDeclaration(ShaderType.U32);
                if (!TypeAtDepth(0).Equals(ShaderType.U32))
                    throw Invalid($"{rwLoad.Name} index must be u32.");
                Emit(rwLoad, ShaderType.F32, [Depth(1), Depth(0)], 2);
                return;
            case ReadWriteStructuredBufferStoreOperation store:
                if (stack.Count < 3 ||
                    !TypeAtDepth(2).Equals(store.BufferPointerType) ||
                    !TypeAtDepth(0).Equals(ShaderType.F32))
                    throw Invalid($"{store.Name} requires an exact writable storage-buffer receiver and f32 value.");
                var converted = ConvertAtDepthForDeclaration(ShaderType.U32, 1);
                if (!TypeAtDepth(0).Equals(ShaderType.U32))
                    throw Invalid($"{store.Name} index must be u32.");
                Emit(
                    store,
                    null,
                    converted
                        ? [Depth(3), Depth(0), Depth(1)]
                        : [Depth(2), Depth(1), Depth(0)],
                    converted ? 4 : 3);
                return;
            case TextureSampleLevelOperation sample:
                if (stack.Count < 4 ||
                    !TypeAtDepth(3).Equals(sample.TexturePointerType) ||
                    !TypeAtDepth(2).Equals(sample.SamplerPointerType) ||
                    !TypeAtDepth(1).Equals(ShaderType.Vec2F32) ||
                    !TypeAtDepth(0).Equals(ShaderType.F32))
                    throw Invalid(
                        $"{sample.Name} requires exact texture, sampler, vec2<f32>, and f32 operands.");
                Emit(
                    sample,
                    ShaderType.Vec4F32,
                    [Depth(3), Depth(2), Depth(1), Depth(0)],
                    4);
                return;
            case IBinaryExpressionOperation binary:
                var (left, right) = TopBinaryTypes();
                if (!left.Equals(binary.LeftType) || !right.Equals(binary.RightType))
                    throw Invalid($"{binary.Name} operation stack: {left.Name}, {right.Name}.");
                EmitBinary(binary);
                NormalizeTop(binary.ResultType);
                return;
            case IBinaryStatementOperation statement:
                var (statementLeft, statementRight) = TopBinaryTypes();
                if (!TypeMatchesIntrinsic(statementLeft, statement.LeftType) ||
                    !TypeMatchesIntrinsic(statementRight, statement.RightType))
                    throw Invalid(
                        $"{statement.Name} operation stack: {statementLeft.Name}, {statementRight.Name}.");
                Emit(statement, null, [Depth(1), Depth(0)], 2);
                return;
            case IUnaryExpressionOperation unary:
                if (!TypeMatchesIntrinsic(TopType(), unary.SourceType))
                    throw Invalid($"{unary.Name} operation stack: {TopType().Name}.");
                Emit(unary, unary.ResultType, [Depth(0)], 1);
                NormalizeTop(unary.ResultType);
                return;
            default:
                throw new NotSupportedException($"Operation intrinsic {operation.Name} is not supported.");
        }
    }

    private static bool TypeMatchesIntrinsic(IShaderType actual, IShaderType expected) =>
        actual.Equals(expected) ||
        actual is IPtrType actualPointer &&
        expected is IPtrType expectedPointer &&
        actualPointer.BaseType.Equals(expectedPointer.BaseType);

    private void NormalizeTop(IShaderType declaredType)
    {
        var operation = StackConversion(declaredType);
        if (operation is not null)
            Emit(operation, operation.ResultType, [Depth(0)], 1);
    }

    private void ConvertTopForDeclaration(IShaderType declaredType) =>
        _ = ConvertAtDepthForDeclaration(declaredType, 0);

    private bool ConvertAtDepthForDeclaration(IShaderType declaredType, int depth)
    {
        var source = TypeAtDepth(depth);
        var operation = DeclarationConversion(declaredType, source);
        if (operation is null)
            return false;
        Emit(operation, operation.ResultType, [Depth(depth)], depth == 0 ? 1 : 0);
        return true;
    }

    private static IUnaryExpressionOperation? StackConversion(IShaderType type) =>
        type switch
        {
            BoolType => ScalarConversionOperation<BoolType, IntType<N32>>.Instance,
            IntType<N8> => ScalarConversionOperation<IntType<N8>, IntType<N32>>.Instance,
            IntType<N16> => ScalarConversionOperation<IntType<N16>, IntType<N32>>.Instance,
            UIntType<N8> => ScalarConversionOperation<UIntType<N8>, IntType<N32>>.Instance,
            UIntType<N16> => ScalarConversionOperation<UIntType<N16>, IntType<N32>>.Instance,
            UIntType<N32> => ScalarConversionOperation<UIntType<N32>, IntType<N32>>.Instance,
            UIntType<N64> => ScalarConversionOperation<UIntType<N64>, IntType<N64>>.Instance,
            _ => null
        };

    private IUnaryExpressionOperation? DeclarationConversion(IShaderType target, IShaderType source)
    {
        if (source.Equals(target))
            return null;
        return (target, source) switch
        {
            (BoolType, IntType<N32>) => ScalarConversionOperation<IntType<N32>, BoolType>.Instance,
            (IntType<N8>, IntType<N32>) => ScalarConversionOperation<IntType<N32>, IntType<N8>>.Instance,
            (IntType<N16>, IntType<N32>) => ScalarConversionOperation<IntType<N32>, IntType<N16>>.Instance,
            (UIntType<N8>, IntType<N32>) => ScalarConversionOperation<IntType<N32>, UIntType<N8>>.Instance,
            (UIntType<N16>, IntType<N32>) => ScalarConversionOperation<IntType<N32>, UIntType<N16>>.Instance,
            (UIntType<N32>, IntType<N32>) => ScalarConversionOperation<IntType<N32>, UIntType<N32>>.Instance,
            (UIntType<N64>, IntType<N64>) => ScalarConversionOperation<IntType<N64>, UIntType<N64>>.Instance,
            _ => throw Invalid($"Cannot convert {source.Name} to declaration type {target.Name}.")
        };
    }

    private static IUnaryExpressionOperation Conversion(IShaderType source, IShaderType target) =>
        (source, target) switch
        {
            (IntType<N32>, UIntType<N32>) => ScalarConversionOperation<IntType<N32>, UIntType<N32>>.Instance,
            (IntType<N64>, UIntType<N64>) => ScalarConversionOperation<IntType<N64>, UIntType<N64>>.Instance,
            _ => throw new NotSupportedException($"Unsupported explicit conversion {source.Name} -> {target.Name}.")
        };

    private void PushAlias(IShaderValue value)
    {
        var node = new ShaderStackInstruction.PushAlias(Immediate(value));
        Emit(node);
    }

    private void Emit(
        IOperation operation,
        IShaderType? result,
        IEnumerable<ShaderStackOperand> operands,
        int popCount)
    {
        var instruction = Instruction<ShaderStackOperand, IShaderType>.Create(operation, result, operands);
        Emit(new ShaderStackInstruction.Operation(instruction, popCount));
    }

    private void Emit(ShaderStackInstruction node)
    {
        var pre = Stack;
        var post = ShaderStackValidation.Apply(node, pre);
        instructions.Add(new Annotated<ShaderStackInstruction, ShaderStackTransition>(
            node,
            new ShaderStackTransition(pre, post, ShaderStackProvenance.Source(current, ordinal++)),
            CilStagePrettyPrinter.PrintShaderStackInstruction));
        stack.Clear();
        stack.AddRange(post);
    }

    private void SetTerminator(ITerminator<Label, ShaderStackOperand> node)
    {
        if (Terminator is not null)
            throw new InvalidOperationException("A shader-stack terminator was already emitted.");
        Terminator = AnnotateTerminator(node, ShaderStackProvenance.Source(current, ordinal));
    }

    private Annotated<ITerminator<Label, ShaderStackOperand>, ShaderStackTransition> AnnotateTerminator(
        ITerminator<Label, ShaderStackOperand> node,
        ShaderStackProvenance provenance)
    {
        var pre = Stack;
        var post = ShaderStackValidation.Apply(node, pre);
        stack.Clear();
        stack.AddRange(post);
        return new(
            node,
            new ShaderStackTransition(pre, post, provenance),
            CilStagePrettyPrinter.PrintShaderStackTerminator);
    }

    private ShaderStackOperand.Depth Depth(int depth) => ShaderStackOperand.At(depth, TypeAtDepth(depth));
    private static ShaderStackOperand.Immediate Immediate(IShaderValue value) => ShaderStackOperand.Resolved(value);

    private IShaderType TopType() => TypeAtDepth(0);

    private IShaderType TypeAtDepth(int depth)
    {
        if (depth < 0 || depth >= stack.Count)
            throw Invalid($"Stack depth {depth} exceeds stack size {stack.Count}.");
        return stack[stack.Count - 1 - depth];
    }

    private (IShaderType Left, IShaderType Right) TopBinaryTypes()
    {
        if (stack.Count < 2)
            throw Invalid("Binary operation requires two stack values.");
        return (TypeAtDepth(1), TypeAtDepth(0));
    }

    private TControl RequireNativeControl<TControl>(CilInstructionInfo instruction)
        where TControl : CilControlFlow
    {
        if (Control is TControl control)
        {
            var source = control switch
            {
                CilControlFlow.Return value => value.Instruction,
                CilControlFlow.Branch value => value.Instruction,
                CilControlFlow.ConditionalBranch value => value.Instruction,
                CilControlFlow.Switch value => value.Instruction,
                _ => throw new InvalidOperationException($"{typeof(TControl).Name} is not native CIL control.")
            };
            if (source.Equals(instruction) && ReferenceEquals(source.Instruction, instruction.Instruction))
                return control;
        }
        throw Invalid($"CIL control mismatch: expected {typeof(TControl).Name}, got {Control}.");
    }

    private ValidationException Invalid(string message) =>
        new($"{message} (IL_{current.ByteOffset:X4})", Environment.Method);

    private static class TerminatorFactory
    {
        public static ITerminator<Label, ShaderStackOperand> ReturnVoid() =>
            Language.Terminator.B.ReturnVoid<Label, ShaderStackOperand>();

        public static ITerminator<Label, ShaderStackOperand> ReturnExpr(ShaderStackOperand value) =>
            Language.Terminator.B.ReturnExpr<Label, ShaderStackOperand>(value);

        public static ITerminator<Label, ShaderStackOperand> Br(Label target) =>
            Language.Terminator.B.Br<Label, ShaderStackOperand>(target);

        public static ITerminator<Label, ShaderStackOperand> BrIf(
            ShaderStackOperand condition,
            Label trueTarget,
            Label falseTarget) =>
            Language.Terminator.B.BrIf(condition, trueTarget, falseTarget);

        public static ITerminator<Label, ShaderStackOperand> Switch(
            ShaderStackOperand selector,
            ImmutableArray<Label> caseTargets,
            Label defaultTarget) =>
            Language.Terminator.B.Switch(selector, caseTargets, defaultTarget);
    }
}

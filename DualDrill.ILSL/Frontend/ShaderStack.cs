using System.Collections.Immutable;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Frontend;

public abstract record ShaderStackOperand
{
    private protected ShaderStackOperand(IShaderType type) => Type = type;

    public IShaderType Type { get; }

    public sealed record Depth : ShaderStackOperand
    {
        internal Depth(int index, IShaderType type) : base(type)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            Index = index;
        }

        public int Index { get; }
    }

    public sealed record Immediate : ShaderStackOperand
    {
        internal Immediate(IShaderValue value) : base(value.Type)
        {
            if (value is IntermediateValue)
                throw new ArgumentException("Shader-stack immediates cannot contain intermediate values.", nameof(value));
            if (value is not LiteralValue and
                not FunctionDeclaration and
                not ParameterPointerValue and
                not VariablePointerValue and
                not StoragePointerValue)
                throw new ArgumentException("Shader-stack immediates must be literals or stable symbols.", nameof(value));
            Value = value;
        }

        public IShaderValue Value { get; }
    }

    internal static Depth At(int index, IShaderType type) => new(index, type);
    internal static Immediate Resolved(IShaderValue value) => new(value);
}

public sealed record ShaderStackProvenance
{
    internal ShaderStackProvenance(
        int originalIndex,
        int byteStart,
        int byteEnd,
        int expansionOrdinal,
        bool synthetic)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(originalIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(byteStart);
        if (byteEnd < byteStart)
            throw new ArgumentOutOfRangeException(nameof(byteEnd));
        ArgumentOutOfRangeException.ThrowIfNegative(expansionOrdinal);
        OriginalIndex = originalIndex;
        ByteStart = byteStart;
        ByteEnd = byteEnd;
        ExpansionOrdinal = expansionOrdinal;
        Synthetic = synthetic;
    }

    public int OriginalIndex { get; }
    public int ByteStart { get; }
    public int ByteEnd { get; }
    public int ExpansionOrdinal { get; }
    public bool Synthetic { get; }

    internal static ShaderStackProvenance Source(CilInstructionInfo source, int ordinal) =>
        new(source.Index, source.ByteOffset, source.NextByteOffset, ordinal, false);

    internal static ShaderStackProvenance SyntheticFallThrough(CilInstructionInfo anchor) =>
        new(anchor.Index, anchor.ByteOffset, anchor.NextByteOffset, 0, true);
}

public sealed record ShaderStackTransition
{
    internal ShaderStackTransition(
        ImmutableArray<IShaderType> pre,
        ImmutableArray<IShaderType> post,
        ShaderStackProvenance provenance)
    {
        if (pre.IsDefault)
            throw new ArgumentException("Shader-stack Pre must be initialized.", nameof(pre));
        if (post.IsDefault)
            throw new ArgumentException("Shader-stack Post must be initialized.", nameof(post));
        Pre = pre;
        Post = post;
        Provenance = provenance;
    }

    public ImmutableArray<IShaderType> Pre { get; }
    public ImmutableArray<IShaderType> Post { get; }
    public ShaderStackProvenance Provenance { get; }
}

public abstract record ShaderStackInstruction
{
    private ShaderStackInstruction()
    {
    }

    public sealed record Operation : ShaderStackInstruction
    {
        internal Operation(
            Instruction<ShaderStackOperand, IShaderType> instruction,
            int popCount)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(popCount);
            Instruction = instruction;
            PopCount = popCount;
        }

        public Instruction<ShaderStackOperand, IShaderType> Instruction { get; }
        public int PopCount { get; }
    }

    public sealed record PushAlias : ShaderStackInstruction
    {
        internal PushAlias(ShaderStackOperand.Immediate value)
        {
            if (value.Value is not (ParameterPointerValue or VariablePointerValue or StoragePointerValue) ||
                value.Type is not IPtrType)
                throw new ArgumentException(
                    "Shader-stack aliases must be stable pointer addresses.",
                    nameof(value));
            Value = value;
        }

        public ShaderStackOperand.Immediate Value { get; }
    }

    public sealed record Duplicate : ShaderStackInstruction;

    public sealed record Drop : ShaderStackInstruction
    {
        internal Drop()
        {
        }
    }
}

public sealed record ShaderStackBasicBlock : ILabeledEntity
{
    internal ShaderStackBasicBlock(
        Label label,
        ImmutableArray<IShaderType> entryStack,
        Seq<Annotated<ShaderStackInstruction, ShaderStackTransition>,
            Annotated<ITerminator<Label, ShaderStackOperand>, ShaderStackTransition>> body)
    {
        Label = label;
        EntryStack = entryStack;
        Body = body;
        Validate();
    }

    public Label Label { get; }
    public ImmutableArray<IShaderType> EntryStack { get; }
    public Seq<Annotated<ShaderStackInstruction, ShaderStackTransition>,
        Annotated<ITerminator<Label, ShaderStackOperand>, ShaderStackTransition>> Body
    { get; }
    public ISuccessor Successor => Body.Last.Node.ToSuccessor();
    public ImmutableArray<IShaderType> ExitStack => Body.Last.Annotation.Post;

    private void Validate()
    {
        var stack = EntryStack;
        foreach (var annotated in Body.Elements)
        {
            if (!stack.SequenceEqual(annotated.Annotation.Pre))
                throw new ArgumentException("Shader-stack instruction Pre does not match the preceding Post.", nameof(Body));
            stack = ShaderStackValidation.Apply(annotated.Node, stack);
            if (!stack.SequenceEqual(annotated.Annotation.Post))
                throw new ArgumentException("Shader-stack instruction Post does not match its derived transition.", nameof(Body));
        }

        var terminator = Body.Last;
        if (!stack.SequenceEqual(terminator.Annotation.Pre))
            throw new ArgumentException("Shader-stack terminator Pre does not match the preceding Post.", nameof(Body));
        stack = ShaderStackValidation.Apply(terminator.Node, stack);
        if (!stack.SequenceEqual(terminator.Annotation.Post))
            throw new ArgumentException("Shader-stack terminator Post does not match its derived transition.", nameof(Body));
    }
}

internal static class ShaderStackValidation
{
    public static ImmutableArray<IShaderType> Apply(
        ShaderStackInstruction instruction,
        ImmutableArray<IShaderType> stack) =>
        instruction switch
        {
            ShaderStackInstruction.Operation operation => Apply(operation, stack),
            ShaderStackInstruction.PushAlias alias => stack.Add(alias.Value.Type),
            ShaderStackInstruction.Duplicate => stack.Length > 0
                ? stack.Add(stack[^1])
                : throw new ArgumentException("Cannot duplicate an empty shader stack."),
            ShaderStackInstruction.Drop => stack.Length > 0
                ? stack.RemoveAt(stack.Length - 1)
                : throw new ArgumentException("Cannot drop from an empty shader stack."),
            _ => throw new NotSupportedException($"Unknown shader-stack instruction {instruction.GetType().FullName}.")
        };

    public static ImmutableArray<IShaderType> Apply(
        ITerminator<Label, ShaderStackOperand> terminator,
        ImmutableArray<IShaderType> stack) =>
        terminator.Evaluate(new TerminatorValidator(stack));

    private static ImmutableArray<IShaderType> Apply(
        ShaderStackInstruction.Operation operation,
        ImmutableArray<IShaderType> stack)
    {
        if (operation.PopCount > stack.Length)
            throw new ArgumentException("Shader-stack operation pops beyond its Pre stack.");
        foreach (var operand in operation.Instruction.Operands)
            ValidateOperand(operand, stack);
        OperationValidator.Validate(operation.Instruction);

        var post = stack.RemoveRange(stack.Length - operation.PopCount, operation.PopCount);
        return operation.Instruction.Result is { } result && result is not UnitType
            ? post.Add(result)
            : post;
    }

    internal static void ValidateOperand(ShaderStackOperand operand, ImmutableArray<IShaderType> stack)
    {
        if (operand is not ShaderStackOperand.Depth depth)
            return;
        if (depth.Index >= stack.Length)
            throw new ArgumentException($"Shader-stack depth {depth.Index} exceeds stack size {stack.Length}.");
        var actual = stack[stack.Length - 1 - depth.Index];
        if (!actual.Equals(depth.Type))
            throw new ArgumentException(
                $"Shader-stack depth {depth.Index} claims {depth.Type.Name}, but contains {actual.Name}.");
    }

    private sealed class TerminatorValidator(ImmutableArray<IShaderType> stack)
        : ITerminatorSemantic<Label, ShaderStackOperand, ImmutableArray<IShaderType>>
    {
        public ImmutableArray<IShaderType> ReturnVoid()
        {
            if (!stack.IsEmpty)
                throw new ArgumentException("Void return requires an empty shader stack.");
            return stack;
        }

        public ImmutableArray<IShaderType> ReturnExpr(ShaderStackOperand expr)
        {
            ValidateOperand(expr, stack);
            if (expr is not ShaderStackOperand.Depth { Index: 0 } || stack.Length != 1)
                throw new ArgumentException("Expression return must consume the only stack value.");
            return [];
        }

        public ImmutableArray<IShaderType> Br(Label target) => stack;

        public ImmutableArray<IShaderType> BrIf(
            ShaderStackOperand condition,
            Label trueTarget,
            Label falseTarget)
        {
            ValidateOperand(condition, stack);
            if (condition is not ShaderStackOperand.Depth { Index: 0 } ||
                !condition.Type.Equals(ShaderType.Bool))
                throw new ArgumentException("Conditional branch must consume a bool from stack depth zero.");
            return stack.RemoveAt(stack.Length - 1);
        }

        public ImmutableArray<IShaderType> Switch(
            ShaderStackOperand selector,
            IReadOnlyList<Label> caseTargets,
            Label defaultTarget)
        {
            ValidateOperand(selector, stack);
            if (selector is not ShaderStackOperand.Depth { Index: 0 } ||
                !selector.Type.Equals(ShaderType.I32))
                throw new ArgumentException("Switch must consume an i32 selector from stack depth zero.");
            return stack.RemoveAt(stack.Length - 1);
        }
    }
}

internal static class OperationValidator
{
    public static void Validate(Instruction<ShaderStackOperand, IShaderType> instruction)
    {
        var operands = instruction.Operands.ToImmutableArray();
        switch (instruction.Operation)
        {
            case NopOperation:
                Require(instruction, [], null);
                return;
            case LiteralOperation:
                if (operands is not [ShaderStackOperand.Immediate { Value: LiteralValue literal }] ||
                    instruction.Result is null ||
                    !literal.Type.Equals(instruction.Result))
                    throw Invalid(instruction);
                return;
            case LoadOperation:
                if (operands is not [{ Type: IPtrType loadPointer }] ||
                    instruction.Result is null ||
                    !loadPointer.BaseType.Equals(instruction.Result))
                    throw Invalid(instruction);
                return;
            case StoreOperation:
                if (operands is not [{ Type: IPtrType storePointer }, var value] ||
                    !storePointer.BaseType.Equals(value.Type) ||
                    instruction.Result is not null)
                    throw Invalid(instruction);
                return;
            case CallOperation call:
                if (operands is not [ShaderStackOperand.Immediate { Value: FunctionDeclaration function }, ..] ||
                    !function.Type.Equals(call.CalleeType) ||
                    !operands.Skip(1).Select(value => value.Type).SequenceEqual(call.CalleeType.ParameterTypes) ||
                    instruction.Result is null ||
                    !instruction.Result.Equals(call.ResultType))
                    throw Invalid(instruction);
                return;
            case AddressOfMemberOperation member:
                if (operands is not [{ Type: IPtrType owner }] ||
                    owner.BaseType is not StructureType ||
                    instruction.Result is not IPtrType result ||
                    !result.BaseType.Equals(member.Member.Type) ||
                    !result.AddressSpace.Equals(owner.AddressSpace))
                    throw Invalid(instruction);
                return;
            case IUnaryExpressionOperation unary:
                Require(
                    instruction,
                    [unary.SourceType],
                    unary.ResultType,
                    allowPointerAddressSpace: unary.SourceType is IPtrType);
                return;
            case IBinaryExpressionOperation binary:
                Require(instruction, [binary.LeftType, binary.RightType], binary.ResultType);
                return;
            case IBinaryStatementOperation statement:
                Require(instruction, [statement.LeftType, statement.RightType], null, allowPointerAddressSpace: true);
                return;
            case IReadOnlyStructuredBufferLengthOperation length
                when ReadOnlyStructuredBufferFamily.IsCanonicalLength(length):
                Require(instruction, [length.BufferPointerType], ShaderType.U32);
                return;
            case IReadOnlyStructuredBufferLoadOperation load
                when ReadOnlyStructuredBufferFamily.IsCanonicalLoad(load):
                Require(instruction, [load.BufferPointerType, ShaderType.U32], load.ElementType);
                return;
            case IReadOnlyStructuredBufferLengthOperation or IReadOnlyStructuredBufferLoadOperation:
                throw Invalid(instruction);
            case ReadWriteStructuredBufferLengthOperation rwLength:
                Require(instruction, [rwLength.BufferPointerType], ShaderType.U32);
                return;
            case ReadWriteStructuredBufferLoadOperation rwLoad:
                Require(instruction, [rwLoad.BufferPointerType, ShaderType.U32], ShaderType.F32);
                return;
            case ReadWriteStructuredBufferStoreOperation store:
                Require(instruction, [store.BufferPointerType, ShaderType.U32, ShaderType.F32], null);
                return;
            case TextureSampleLevelOperation sample:
                Require(
                    instruction,
                    [
                        sample.TexturePointerType,
                        sample.SamplerPointerType,
                        ShaderType.Vec2F32,
                        ShaderType.F32
                    ],
                    ShaderType.Vec4F32);
                return;
            case VectorCompositeConstructionOperation vector:
                Require(instruction, vector.ParameterTypes, vector.ResultType);
                return;
            case ZeroConstructorOperation zero:
                Require(instruction, [], zero.ResultType);
                return;
            default:
                throw new NotSupportedException(
                    $"Shader-stack validation does not support operation {instruction.Operation.GetType().FullName}.");
        }
    }

    private static void Require(
        Instruction<ShaderStackOperand, IShaderType> instruction,
        IReadOnlyList<IShaderType> operandTypes,
        IShaderType? resultType,
        bool allowPointerAddressSpace = false)
    {
        var actual = instruction.Operands.Select(value => value.Type).ToImmutableArray();
        if (actual.Length != operandTypes.Count ||
            actual.Where((type, index) =>
                    !TypeMatches(type, operandTypes[index], allowPointerAddressSpace))
                .Any() ||
            (resultType is null
                ? instruction.Result is not null
                : instruction.Result is null || !instruction.Result.Equals(resultType)))
            throw Invalid(instruction);
    }

    private static bool TypeMatches(IShaderType actual, IShaderType expected, bool allowPointerAddressSpace) =>
        actual.Equals(expected) ||
        allowPointerAddressSpace &&
        actual is IPtrType actualPointer &&
        expected is IPtrType expectedPointer &&
        actualPointer.BaseType.Equals(expectedPointer.BaseType);

    private static ArgumentException Invalid(Instruction<ShaderStackOperand, IShaderType> instruction) =>
        new(
            $"Invalid typed shader-stack operation {instruction.Operation.Name}: " +
            $"operands=[{string.Join(", ", instruction.Operands.Select(value => value.Type.Name))}], " +
            $"result={instruction.Result?.Name ?? "<none>"}.");
}

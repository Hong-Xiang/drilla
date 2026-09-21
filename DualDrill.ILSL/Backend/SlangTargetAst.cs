using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Globalization;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Backend;

public sealed class SlangFunctionBody : IFunctionBody, ILocalDeclarationContext
{
    private readonly ImmutableDictionary<Label, int> labelIndices;
    private readonly ImmutableDictionary<IShaderValue, int> valueIndices;

    public SlangFunctionBody(FunctionDeclaration declaration, SlangBlock body)
        : this(declaration, body, SlangLoweringOrigins.Empty)
    {
    }

    internal SlangFunctionBody(
        FunctionDeclaration declaration,
        SlangBlock body,
        SlangLoweringOrigins origins)
    {
        Declaration = declaration;
        Body = body;
        Origins = origins;
        LocalVariables = [.. Statements(body).OfType<SlangDeclare>().Select(statement => statement.Variable)];
        Labels = [.. Statements(body)
            .Select(statement => statement switch
            {
                SlangScope { OriginalLabel: { } label } => label,
                SlangLoop { OriginalLabel: { } label } => label,
                _ => null
            })
            .OfType<Label>()
            .Distinct()];
        var valueIndexBuilder =
            ImmutableDictionary.CreateBuilder<IShaderValue, int>(ReferenceEqualityComparer.Instance);
        foreach (var value in Statements(body).SelectMany(Values)
                     .Where(value => value is not LiteralValue and not FunctionDeclaration and not ParameterPointerValue))
            valueIndexBuilder.TryAdd(value, valueIndexBuilder.Count);
        valueIndices = valueIndexBuilder.ToImmutable();
        labelIndices = Labels.Select((label, index) => (label, index))
            .ToImmutableDictionary(item => item.label, item => item.index);
    }

    public FunctionDeclaration Declaration { get; }
    public SlangBlock Body { get; }
    internal SlangLoweringOrigins Origins { get; }
    public ILocalDeclarationContext DeclarationContext => this;
    public ImmutableArray<VariableDeclaration> LocalVariables { get; }
    public ImmutableArray<Label> Labels { get; }

    public int LabelIndex(Label label) => labelIndices[label];
    public int ValueIndex(IShaderValue value) => valueIndices[value];
    public int VariableIndex(VariableDeclaration variable) => LocalVariables.IndexOf(variable);

    public string PrettyPrint()
    {
        using var text = new StringWriter();
        using var writer = new IndentedTextWriter(text);
        Dump(writer);
        return text.ToString();
    }

    public void Dump(IndentedTextWriter writer)
    {
        writer.Write("slang-target ");
        writer.WriteLine(Declaration.Name);
        DumpBlock(writer, Body);
    }

    private void DumpBlock(IndentedTextWriter writer, SlangBlock block)
    {
        foreach (var statement in block.Statements)
        {
            switch (statement)
            {
                case SlangDeclare declare:
                    writer.Write("declare ");
                    DumpPlace(writer, new SlangVariablePlace(declare.Variable));
                    writer.Write(" : ");
                    writer.WriteLine(declare.Variable.Type.Name);
                    break;
                case SlangBind bind:
                    writer.Write("bind ");
                    DumpValue(writer, bind.Instruction.Result!);
                    writer.Write(" : ");
                    writer.Write(bind.Instruction.Result!.Type.Name);
                    writer.Write(" = ");
                    DumpInstruction(writer, bind.Instruction);
                    writer.WriteLine();
                    break;
                case SlangEffect effect:
                    writer.Write("effect ");
                    DumpInstruction(writer, effect.Instruction);
                    writer.WriteLine();
                    break;
                case SlangAssign assign:
                    writer.Write("assign ");
                    DumpPlace(writer, assign.Target);
                    writer.Write(" <- ");
                    DumpOperand(writer, assign.Value);
                    writer.WriteLine();
                    break;
                case SlangGetDimensions dimensions:
                    writer.Write("dimensions ");
                    DumpValue(writer, dimensions.Count);
                    writer.Write(", ");
                    DumpValue(writer, dimensions.Stride);
                    writer.Write(" <- ");
                    DumpOperand(writer, dimensions.Buffer);
                    writer.WriteLine();
                    break;
                case SlangScope scope:
                    writer.Write("scope");
                    DumpLabel(writer, scope.OriginalLabel);
                    writer.WriteLine();
                    using (writer.IndentedScope())
                        DumpBlock(writer, scope.Body);
                    break;
                case SlangIf conditional:
                    writer.Write("if ");
                    DumpOperand(writer, conditional.Condition);
                    writer.WriteLine();
                    using (writer.IndentedScope())
                    {
                        writer.WriteLine("then");
                        using (writer.IndentedScope())
                            DumpBlock(writer, conditional.WhenTrue);
                        writer.WriteLine("else");
                        using (writer.IndentedScope())
                            DumpBlock(writer, conditional.WhenFalse);
                    }
                    break;
                case SlangDoOnce once:
                    writer.WriteLine("once");
                    using (writer.IndentedScope())
                        DumpBlock(writer, once.Body);
                    break;
                case SlangLoop loop:
                    writer.Write("loop");
                    DumpLabel(writer, loop.OriginalLabel);
                    writer.WriteLine();
                    using (writer.IndentedScope())
                        DumpBlock(writer, loop.Body);
                    break;
                case SlangReturnValue returned:
                    writer.Write("return ");
                    DumpOperand(writer, returned.Value);
                    writer.WriteLine();
                    break;
                case SlangReturnVoid:
                    writer.WriteLine("return");
                    break;
                case SlangBreak:
                    writer.WriteLine("break");
                    break;
                case SlangContinue:
                    writer.WriteLine("continue");
                    break;
                default:
                    throw new NotSupportedException($"Unknown Slang statement {statement.GetType().Name}.");
            }
        }
    }

    private void DumpInstruction(
        IndentedTextWriter writer,
        Instruction<SlangOperand, IShaderValue> instruction)
    {
        writer.Write(instruction.Operation.Name);
        foreach (var operand in instruction.Operands)
        {
            writer.Write(" (");
            DumpOperand(writer, operand);
            writer.Write(')');
        }
    }

    private void DumpOperand(IndentedTextWriter writer, SlangOperand operand)
    {
        switch (operand)
        {
            case SlangValueOperand value:
                DumpValue(writer, value.Value);
                break;
            case SlangPlaceOperand place:
                writer.Write("read ");
                DumpPlace(writer, place.Place);
                break;
            default:
                throw new NotSupportedException($"Unknown Slang operand {operand.GetType().Name}.");
        }
    }

    private void DumpPlace(IndentedTextWriter writer, SlangPlace place)
    {
        switch (place)
        {
            case SlangVariablePlace variable:
                DumpValue(writer, variable.Variable.Value);
                break;
            case SlangParameterPlace parameter:
                writer.Write('$');
                writer.Write(parameter.Parameter.Name);
                break;
            case SlangMemberPlace member:
                DumpPlace(writer, member.Target);
                writer.Write('.');
                writer.Write(member.Member.Name);
                break;
            case SlangComponentPlace component:
                DumpPlace(writer, component.Target);
                writer.Write('.');
                writer.Write(component.Component);
                break;
            case SlangSwizzlePlace swizzle:
                DumpPlace(writer, swizzle.Target);
                writer.Write('.');
                writer.Write(swizzle.Pattern);
                break;
            case SlangIndexedPlace indexed:
                DumpPlace(writer, indexed.Target);
                writer.Write('[');
                DumpOperand(writer, indexed.Index);
                writer.Write(']');
                break;
            default:
                throw new NotSupportedException($"Unknown Slang place {place.GetType().Name}.");
        }
    }

    private void DumpValue(IndentedTextWriter writer, IShaderValue value)
    {
        if (value is LiteralValue literal)
        {
            writer.Write(SlangLiteralFormatter.Dump(literal.Value));
            return;
        }

        if (value is FunctionDeclaration function)
        {
            writer.Write('@');
            writer.Write(function.Name);
            return;
        }

        writer.Write('%');
        writer.Write(ValueIndex(value));
        if (value is VariablePointerValue { Declaration.Name.Length: > 0 } variable)
        {
            writer.Write('(');
            writer.Write(variable.Declaration.Name);
            writer.Write(')');
        }
    }

    private void DumpLabel(IndentedTextWriter writer, Label? label)
    {
        if (label is null) return;
        writer.Write(" ^");
        writer.Write(LabelIndex(label));
        writer.Write('(');
        writer.Write(label.Name);
        writer.Write(')');
    }

    private static IEnumerable<SlangStatement> Statements(SlangBlock block)
    {
        foreach (var statement in block.Statements)
        {
            yield return statement;
            switch (statement)
            {
                case SlangScope scope:
                    foreach (var nested in Statements(scope.Body)) yield return nested;
                    break;
                case SlangIf conditional:
                    foreach (var nested in Statements(conditional.WhenTrue)) yield return nested;
                    foreach (var nested in Statements(conditional.WhenFalse)) yield return nested;
                    break;
                case SlangDoOnce once:
                    foreach (var nested in Statements(once.Body)) yield return nested;
                    break;
                case SlangLoop loop:
                    foreach (var nested in Statements(loop.Body)) yield return nested;
                    break;
            }
        }
    }

    private static IEnumerable<IShaderValue> Values(SlangStatement statement) =>
        statement switch
        {
            SlangDeclare declare => [declare.Variable.Value],
            SlangBind bind => [bind.Instruction.Result!, .. bind.Instruction.Operands.SelectMany(Values)],
            SlangEffect effect => effect.Instruction.Operands.SelectMany(Values),
            SlangAssign assign => [.. Values(assign.Target), .. Values(assign.Value)],
            SlangGetDimensions dimensions =>
                [dimensions.Count, dimensions.Stride, .. Values(dimensions.Buffer)],
            SlangIf conditional => Values(conditional.Condition),
            SlangReturnValue returned => Values(returned.Value),
            _ => []
        };

    private static IEnumerable<IShaderValue> Values(SlangOperand operand) =>
        operand switch
        {
            SlangValueOperand value => [value.Value],
            SlangPlaceOperand place => Values(place.Place),
            _ => []
        };

    private static IEnumerable<IShaderValue> Values(SlangPlace place) =>
        place switch
        {
            SlangVariablePlace variable => [variable.Variable.Value],
            SlangMemberPlace member => Values(member.Target),
            SlangComponentPlace component => Values(component.Target),
            SlangSwizzlePlace swizzle => Values(swizzle.Target),
            SlangIndexedPlace indexed => [.. Values(indexed.Target), .. Values(indexed.Index)],
            _ => []
        };
}

internal sealed record SlangContinuationOrigin(
    Label Target,
    Label Owner,
    ScopedContinuationKind Kind);

internal sealed record SlangParameterOrigin(
    Label Label,
    IShaderValue Parameter,
    VariableDeclaration Slot,
    SlangBind Definition,
    SlangAssign? Capture);

internal sealed record SlangDefinitionOrigin(
    Label Label,
    int InstructionOrdinal,
    Instruction<IShaderValue, IShaderValue> Source,
    SlangBind Definition,
    SlangAssign? Capture);

internal sealed record SlangDimensionsOrigin(
    Label Label,
    int InstructionOrdinal,
    Instruction<IShaderValue, IShaderValue> Source,
    SlangGetDimensions Dimensions,
    SlangAssign? Capture);

internal sealed record SlangInstructionOrigin(
    Label Label,
    int InstructionOrdinal,
    Instruction<IShaderValue, IShaderValue> Source,
    SlangStatement Target);

internal sealed record SlangTransferArgumentOrigin(
    int Position,
    IShaderValue Argument,
    IShaderValue Snapshot,
    SlangBind Definition,
    IShaderValue Parameter,
    VariableDeclaration Slot,
    SlangAssign Assignment);

internal sealed record SlangTransferOrigin(
    Label Source,
    int Arm,
    RegionJump<IShaderValue> Jump,
    SlangContinuationOrigin Continuation,
    int TokenId,
    ImmutableArray<SlangTransferArgumentOrigin> Arguments,
    SlangAssign TokenAssignment,
    SlangBreak Break);

internal sealed record SlangConditionalOrigin(
    Label Source,
    IShaderValue Condition,
    SlangIf Conditional);

internal sealed record SlangGateOrigin(
    SlangContinuationOrigin Continuation,
    int TokenId,
    SlangBind Comparison,
    SlangIf Conditional);

internal sealed record SlangReturnOrigin(
    Label Label,
    IShaderValue? Value,
    SlangStatement Return);

internal sealed record SlangHoistedReturnOrigin(
    Label Label,
    IShaderValue Value,
    VariableDeclaration Slot,
    int TokenId,
    SlangAssign ValueAssignment,
    SlangAssign TokenAssignment,
    SlangBreak Break);

internal sealed record SlangReturnEpilogueOrigin(
    VariableDeclaration Slot,
    int TokenId,
    SlangReturnValue Return);

internal sealed record SlangCarrierBreakOrigin(
    Label Owner,
    Label NextBinding,
    SlangDoOnce Carrier,
    SlangBreak Break);

internal sealed record SlangLoweringOrigins(
    VariableDeclaration? ControlToken,
    VariableDeclaration? ReturnValueSlot,
    int? ReturnTokenId,
    ImmutableDictionary<IShaderValue, VariableDeclaration> ParameterSlots,
    ImmutableDictionary<IShaderValue, VariableDeclaration> Captures,
    ImmutableArray<SlangParameterOrigin> Parameters,
    ImmutableArray<SlangDefinitionOrigin> Definitions,
    ImmutableArray<SlangDimensionsOrigin> Dimensions,
    ImmutableArray<SlangInstructionOrigin> Instructions,
    ImmutableArray<SlangTransferOrigin> Transfers,
    ImmutableArray<SlangConditionalOrigin> Conditionals,
    ImmutableArray<SlangGateOrigin> Gates,
    ImmutableArray<SlangReturnOrigin> Returns,
    ImmutableArray<SlangHoistedReturnOrigin> HoistedReturns,
    SlangReturnEpilogueOrigin? ReturnEpilogue,
    ImmutableArray<SlangCarrierBreakOrigin> CarrierBreaks)
{
    internal static SlangLoweringOrigins Empty { get; } = new(
        null,
        null,
        null,
        ImmutableDictionary<IShaderValue, VariableDeclaration>.Empty,
        ImmutableDictionary<IShaderValue, VariableDeclaration>.Empty,
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        null,
        []);
}

public sealed record SlangBlock(ImmutableArray<SlangStatement> Statements)
{
    public static SlangBlock Empty { get; } = new([]);
}

public abstract record SlangStatement;

public sealed record SlangDeclare(VariableDeclaration Variable) : SlangStatement;

public sealed record SlangBind : SlangStatement
{
    public SlangBind(Instruction<SlangOperand, IShaderValue> instruction)
    {
        if (instruction.Result is null)
            throw new ArgumentException("A Slang binding requires an instruction result.", nameof(instruction));
        Instruction = instruction;
    }

    public Instruction<SlangOperand, IShaderValue> Instruction { get; }
}

public sealed record SlangEffect : SlangStatement
{
    public SlangEffect(Instruction<SlangOperand, IShaderValue> instruction)
    {
        if (instruction.Result is { Type: not UnitType })
            throw new ArgumentException("A Slang effect cannot discard an instruction result.", nameof(instruction));
        Instruction = instruction;
    }

    public Instruction<SlangOperand, IShaderValue> Instruction { get; }
}

public sealed record SlangAssign : SlangStatement
{
    public SlangAssign(SlangPlace target, SlangOperand value)
    {
        if (!Equals(target.Type, value.Type))
            throw new ArgumentException(
                $"Cannot assign {value.Type.Name} to {target.Type.Name}.",
                nameof(value));
        Target = target;
        Value = value;
    }

    public SlangPlace Target { get; }
    public SlangOperand Value { get; }
}

public sealed record SlangGetDimensions(
    SlangOperand Buffer,
    IShaderValue Count,
    IShaderValue Stride) : SlangStatement;

public sealed record SlangScope(Label? OriginalLabel, SlangBlock Body) : SlangStatement;
public sealed record SlangIf(SlangOperand Condition, SlangBlock WhenTrue, SlangBlock WhenFalse) : SlangStatement;
public sealed record SlangDoOnce(SlangBlock Body) : SlangStatement;
public sealed record SlangLoop(Label? OriginalLabel, SlangBlock Body) : SlangStatement;
public sealed record SlangReturnValue(SlangOperand Value) : SlangStatement;
public sealed record SlangReturnVoid : SlangStatement;
public sealed record SlangBreak : SlangStatement;
public sealed record SlangContinue : SlangStatement;

public abstract record SlangOperand
{
    public abstract IShaderType Type { get; }
}

public sealed record SlangValueOperand(IShaderValue Value) : SlangOperand
{
    public override IShaderType Type => Value.Type;
}

public sealed record SlangPlaceOperand(SlangPlace Place) : SlangOperand
{
    public override IShaderType Type => Place.Type;
}

public abstract record SlangPlace
{
    public abstract IShaderType Type { get; }
}

public sealed record SlangVariablePlace(VariableDeclaration Variable) : SlangPlace
{
    public override IShaderType Type => Variable.Type;
}

public sealed record SlangParameterPlace(ParameterDeclaration Parameter) : SlangPlace
{
    public override IShaderType Type => Parameter.Type;
}

public sealed record SlangMemberPlace(SlangPlace Target, MemberDeclaration Member) : SlangPlace
{
    public override IShaderType Type => Member.Type;
}

public sealed record SlangComponentPlace(
    SlangPlace Target,
    string Component,
    IShaderType ComponentType) : SlangPlace
{
    public override IShaderType Type => ComponentType;
}

public sealed record SlangSwizzlePlace(
    SlangPlace Target,
    string Pattern,
    IShaderType SwizzleType) : SlangPlace
{
    public override IShaderType Type => SwizzleType;
}

public sealed record SlangIndexedPlace(
    SlangPlace Target,
    SlangOperand Index,
    IShaderType ElementType) : SlangPlace
{
    public override IShaderType Type => ElementType;
}

internal static class SlangLiteralFormatter
{
    public static string Source(ILiteral literal) => Format(literal, string.Empty);

    public static string Dump(ILiteral literal) => Format(literal, literal switch
    {
        BoolLiteral => "_b",
        I32Literal => "_i32",
        I64Literal => "_i64",
        U32Literal => "_u32",
        U64Literal => "_u64",
        F32Literal => "_f32",
        F64Literal => "_f64",
        _ => throw new NotSupportedException($"Unknown Slang literal {literal.GetType().Name}.")
    });

    private static string Format(ILiteral literal, string suffix) =>
        literal switch
        {
            BoolLiteral value => $"{(value.Value ? "true" : "false")}{suffix}",
            I32Literal value => $"{value.Value.ToString(CultureInfo.InvariantCulture)}{suffix}",
            I64Literal value => $"{value.Value.ToString(CultureInfo.InvariantCulture)}{suffix}",
            U32Literal value => $"{value.Value.ToString(CultureInfo.InvariantCulture)}{suffix}",
            U64Literal value => $"{value.Value.ToString(CultureInfo.InvariantCulture)}{suffix}",
            F32Literal value => $"{value.Value.ToString(CultureInfo.InvariantCulture)}{suffix}",
            F64Literal value => $"{value.Value.ToString(CultureInfo.InvariantCulture)}{suffix}",
            _ => throw new NotSupportedException($"Unknown Slang literal {literal.GetType().Name}.")
        };

}

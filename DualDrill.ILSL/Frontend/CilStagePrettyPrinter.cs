using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.CodeTextWriter;
using FlowControl = System.Reflection.Emit.FlowControl;

namespace DualDrill.CLSL.Frontend;

public static class CilStagePrettyPrinter
{
    internal static void PrintValueControlFlow(
        CilValueControlFlowBody body,
        IndentedTextWriter writer) =>
        PrintValueControlFlow(body.Graph, body.DeclarationContext, writer);

    public static void PrintValueControlFlow(
        ControlFlowGraph<CilValueBasicBlock> graph,
        ILocalDeclarationContext context,
        IndentedTextWriter writer)
    {
        writer.WriteLine("flat-value-cfg");
        foreach (var label in graph.Labels())
            PrintValueBlock(graph[label], context, writer);
    }

    internal static Action<CilValueBasicBlock, BlockControlFacts, IndentedTextWriter, PrettyPrintOption>
        CreateValueBlockControlFactsPrinter(ILocalDeclarationContext context) =>
        (block, facts, writer, _) =>
        {
            PrintValueBlockHeader(block, context, writer);
            writer.Write(" facts={rpo=");
            writer.Write(Invariant(facts.ReversePostOrderIndex));
            writer.Write(" idom=");
            WriteOptionalLabel(facts.ImmediateDominator, context, writer);
            writer.Write(" postdom=");
            WritePostDominance(facts.PostDominance, context, writer);
            writer.Write(" incoming=[");
            var separator = "";
            foreach (var arm in facts.IncomingArms)
            {
                writer.Write(separator);
                arm.Source.Dump(context, writer);
                writer.Write("[");
                writer.Write(Invariant(arm.SuccessorIndex));
                writer.Write("]:");
                writer.Write(arm.IsBackedge ? "backedge" : "forward");
                separator = ",";
            }
            writer.Write("]");
            writer.Write(" loop-header=");
            writer.Write(facts.IsLoopHeader ? "true" : "false");
            writer.WriteLine("}");
            PrintValueBlockBody(block, context, writer);
        };

    private static void WritePostDominance(
        ExitPostDominance postDominance,
        ILocalDeclarationContext context,
        IndentedTextWriter writer)
    {
        switch (postDominance)
        {
            case ExitPostDominance.Block block:
                block.Target.Dump(context, writer);
                break;
            case ExitPostDominance.FunctionExit:
                writer.Write("function-exit");
                break;
            case ExitPostDominance.NoExitPath:
                writer.Write("no-exit-path");
                break;
            default:
                throw new UnreachableException(
                    $"Unsupported exit-postdominance result {postDominance.GetType().FullName}.");
        }

        writer.Write(postDominance.MayDiverge ? "(may-diverge)" : "(finite)");
    }

    internal static void PrintRawLinearCode(
        LinearCode<CilInstructionInfo> code,
        IndentedTextWriter writer,
        PrettyPrintOption option)
    {
        writer.WriteLine("linear-cil raw (byte ranges are half-open; branch rel is encoded displacement)");
        foreach (var instruction in code.Instructions)
        {
            PrintInstruction(instruction, writer, option);
            writer.WriteLine();
        }
    }

    internal static void PrintPreAnnotatedLinearCode(
        LinearCode<Annotated<CilInstructionInfo, PreStack>> code,
        IndentedTextWriter writer,
        PrettyPrintOption option)
    {
        writer.WriteLine("linear-cil pre-annotated reachable (byte ranges are half-open; stack order: bottom -> top)");
        foreach (var instruction in code.Instructions)
            instruction.PrettyPrint(writer, option);
    }

    internal static void PrintAnnotatedInstruction(
        CilInstructionInfo instruction,
        PreStack pre,
        IndentedTextWriter writer,
        PrettyPrintOption option)
    {
        PrintInstruction(instruction, writer, option);
        writer.Write(" pre=");
        writer.Write(Stack(pre.Types));
        writer.WriteLine();
    }

    internal static void PrintShaderStackControlFlow(
        ShaderStackControlFlowBody body,
        IndentedTextWriter writer)
    {
        PrintShaderStackGraph(body.Graph, writer, PrettyPrintOption.Default);
    }

    internal static void PrintShaderStackBlockList(
        BlockList<ShaderStackBasicBlock> blocks,
        IndentedTextWriter writer,
        PrettyPrintOption option)
    {
        var labelIds = blocks.Blocks.Select((block, index) => (block.Label, index))
                             .ToDictionary(item => item.Label, item => item.index);
        writer.WriteLine("labelled-shader-stack-block-list (stack order: bottom -> top)");
        writer.Write("entry=");
        writer.WriteLine(LabelName(blocks.EntryLabel, labelIds));
        foreach (var block in blocks.Blocks)
            PrintShaderStackBlock(block, labelIds, writer);
    }

    internal static void PrintShaderStackGraph(
        ControlFlowGraph<ShaderStackBasicBlock> graph,
        IndentedTextWriter writer,
        PrettyPrintOption option)
    {
        var labels = graph.Labels()
                          .ToImmutableArray();
        var labelIds = labels.Select((label, index) => (label, index))
                             .ToDictionary(item => item.label, item => item.index);

        writer.WriteLine("shader-stack-cfg (stack order: bottom -> top)");
        foreach (var label in labels)
        {
            var block = graph[label];
            writer.Write(LabelName(label, labelIds));
            writer.Write(" predecessors=");
            writer.Write(LabelList(
                graph.Predecessor(label).OrderBy(predecessor => labelIds[predecessor]),
                labelIds));
            writer.WriteLine();
            PrintShaderStackBlockBody(block, labelIds, writer);
        }
    }

    internal static void PrintShaderStackInstruction(
        ShaderStackInstruction instruction,
        ShaderStackTransition transition,
        IndentedTextWriter writer,
        PrettyPrintOption option)
    {
        PrintProvenance(transition.Provenance, writer);
        writer.Write(" ");
        switch (instruction)
        {
            case ShaderStackInstruction.Operation operation:
                writer.Write(operation.Instruction.Operation.Name);
                writer.Write("(");
                writer.Write(string.Join(", ", operation.Instruction.Operands.Select(OperandName)));
                writer.Write(")");
                writer.Write(" pop=");
                writer.Write(Invariant(operation.PopCount));
                if (operation.Instruction.Result is { } result)
                {
                    writer.Write(" result=");
                    writer.Write(ShaderTypeName(result));
                }
                break;
            case ShaderStackInstruction.PushAlias alias:
                writer.Write("push-alias ");
                writer.Write(OperandName(alias.Value));
                break;
            case ShaderStackInstruction.Drop:
                writer.Write("drop");
                break;
        }
        PrintTransition(transition, writer);
    }

    internal static void PrintShaderStackTerminator(
        ITerminator<Label, ShaderStackOperand> terminator,
        ShaderStackTransition transition,
        IndentedTextWriter writer,
        PrettyPrintOption option)
    {
        PrintProvenance(transition.Provenance, writer);
        writer.Write(" ");
        writer.Write(terminator.Evaluate(new ShaderStackTerminatorFormatter()));
        PrintTransition(transition, writer);
    }

    private static void PrintShaderStackBlock(
        ShaderStackBasicBlock block,
        IReadOnlyDictionary<Label, int> labelIds,
        IndentedTextWriter writer)
    {
        writer.Write(LabelName(block.Label, labelIds));
        writer.Write(" entry=");
        writer.Write(ShaderStack(block.EntryStack));
        writer.WriteLine();
        PrintShaderStackBlockBody(block, labelIds, writer);
    }

    private static void PrintShaderStackBlockBody(
        ShaderStackBasicBlock block,
        IReadOnlyDictionary<Label, int> labelIds,
        IndentedTextWriter writer)
    {
        using (writer.IndentedScope())
        {
            foreach (var instruction in block.Body.Elements)
                instruction.PrettyPrint(writer, PrettyPrintOption.Default);
            var terminator = block.Body.Last;
            PrintProvenance(terminator.Annotation.Provenance, writer);
            writer.Write(" ");
            writer.Write(terminator.Node.Evaluate(new ShaderStackTerminatorFormatter(labelIds)));
            PrintTransition(terminator.Annotation, writer);
        }
    }

    private static void PrintTransition(ShaderStackTransition transition, IndentedTextWriter writer)
    {
        writer.Write(" pre=");
        writer.Write(ShaderStack(transition.Pre));
        writer.Write(" post=");
        writer.Write(ShaderStack(transition.Post));
        writer.WriteLine();
    }

    private static void PrintProvenance(ShaderStackProvenance provenance, IndentedTextWriter writer)
    {
        writer.Write("#");
        writer.Write(Invariant(provenance.OriginalIndex));
        writer.Write(".");
        writer.Write(Invariant(provenance.ExpansionOrdinal));
        writer.Write(" ");
        writer.Write(ByteRange(provenance.ByteStart, provenance.ByteEnd));
        if (provenance.Synthetic)
            writer.Write(" synthetic");
    }

    private static string OperandName(ShaderStackOperand operand) =>
        operand switch
        {
            ShaderStackOperand.Depth depth =>
                $"depth{Invariant(depth.Index)}:{ShaderTypeName(depth.Type)}",
            ShaderStackOperand.Immediate { Value: LiteralValue literal } =>
                literal.Value.ToString() ?? ShaderTypeName(literal.Type),
            ShaderStackOperand.Immediate { Value: FunctionDeclaration function } =>
                $"@fn({function.Name})",
            ShaderStackOperand.Immediate { Value: ParameterPointerValue parameter } =>
                $"&arg({parameter.Declaration.Name})",
            ShaderStackOperand.Immediate { Value: VariablePointerValue variable } =>
                $"&var({variable.Declaration.Name})",
            ShaderStackOperand.Immediate { Value: StoragePointerValue storage } =>
                $"&storage({storage.VariableDeclaration.Name})",
            _ => $"<{ShaderTypeName(operand.Type)}>"
        };

    private static string ShaderStack(IEnumerable<IShaderType> stack) =>
        "[" + string.Join(", ", stack.Select(ShaderTypeName)) + "]";

    private sealed class ShaderStackTerminatorFormatter(
        IReadOnlyDictionary<Label, int>? labelIds = null)
        : ITerminatorSemantic<Label, ShaderStackOperand, string>
    {
        public string ReturnVoid() => "return";
        public string ReturnExpr(ShaderStackOperand expr) => $"return {OperandName(expr)}";
        public string Br(Label target) => $"br {Name(target)}";
        public string BrIf(ShaderStackOperand condition, Label trueTarget, Label falseTarget) =>
            $"br_if {OperandName(condition)} true={Name(trueTarget)} false={Name(falseTarget)}";

        private string Name(Label label) =>
            labelIds is null ? $"^({label.Name})" : LabelName(label, labelIds);
    }

    internal static void PrintBlockList(
        BlockList<CilInstructionBlock> blocks,
        IndentedTextWriter writer,
        PrettyPrintOption option)
    {
        var labelIds = blocks.Blocks.Select((block, index) => (block.Label, index))
                             .ToDictionary(item => item.Label, item => item.index);
        writer.WriteLine("labelled-cil-block-list (storage order; byte ranges are half-open; stack order: bottom -> top)");
        writer.Write("entry=");
        writer.WriteLine(LabelName(blocks.EntryLabel, labelIds));
        foreach (var block in blocks.Blocks)
        {
            PrintBlockHeader(block, labelIds, writer);
            writer.WriteLine();
            PrintBlockControl(block, labelIds, writer);
        }
    }

    private static void PrintBlockHeader(
        CilInstructionBlock block,
        IReadOnlyDictionary<Label, int> labelIds,
        IndentedTextWriter writer)
    {
        writer.Write(LabelName(block.Label, labelIds));
        writer.Write(" instructions=");
        writer.Write(IndexRange(block.InstructionIndex, block.InstructionCount));
        writer.Write(" bytes=");
        writer.Write(ByteRange(block.ByteOffset, block.Instructions[^1].Node.NextByteOffset));
        writer.Write(" entry=");
        writer.Write(Stack(block.EntryStack.Types));
    }

    private static void PrintBlockControl(
        CilInstructionBlock block,
        IReadOnlyDictionary<Label, int> labelIds,
        IndentedTextWriter writer)
    {
        using (writer.IndentedScope())
        {
            writer.Write("control: ");
            PrintControl(block.Terminator, labelIds, writer);
            writer.WriteLine();
        }
    }

    private static void PrintInstruction(
        CilInstructionInfo instruction,
        IndentedTextWriter writer,
        PrettyPrintOption option)
    {
        writer.Write("#");
        writer.Write(Invariant(instruction.Index));
        writer.Write(" ");
        writer.Write(ByteRange(instruction.ByteOffset, instruction.NextByteOffset));
        writer.Write(" ");
        writer.Write(OpCodeName(instruction));

        var operand = FormatOperand(instruction);
        if (operand is not null)
        {
            writer.Write(" ");
            writer.Write(operand);
        }
    }

    private static void PrintControl(
        CilControlFlow control,
        IReadOnlyDictionary<Label, int> labelIds,
        IndentedTextWriter writer)
    {
        switch (control)
        {
            case CilControlFlow.Return returned:
                writer.Write("native ");
                writer.Write(OpCodeName(returned.Instruction));
                return;
            case CilControlFlow.Branch branch:
                writer.Write("native ");
                writer.Write(OpCodeName(branch.Instruction));
                writer.Write(" ");
                writer.Write(FormatRelativeBranch(branch.Instruction));
                writer.Write(" target=");
                writer.Write(LabelName(branch.Target, labelIds));
                return;
            case CilControlFlow.ConditionalBranch branch:
                writer.Write("native ");
                writer.Write(OpCodeName(branch.Instruction));
                writer.Write(" ");
                writer.Write(FormatRelativeBranch(branch.Instruction));
                writer.Write(" taken=");
                writer.Write(LabelName(branch.BranchTarget, labelIds));
                writer.Write(" fallthrough=");
                writer.Write(LabelName(branch.FallThroughTarget, labelIds));
                return;
            case CilControlFlow.FallThrough fallThrough:
                writer.Write("synthetic fallthrough target=");
                writer.Write(LabelName(fallThrough.Target, labelIds));
                return;
            case CilControlFlow.EndOfCode:
                writer.Write("synthetic end-of-code");
                return;
            default:
                throw new NotSupportedException(
                    $"Unsupported CIL control diagnostic variant {control.GetType().FullName}.");
        }
    }

    private static string? FormatOperand(CilInstructionInfo instruction)
    {
        var operand = instruction.Instruction.Operand;
        if (operand is null)
            return null;

        if (instruction.Instruction.OpCode.FlowControl is FlowControl.Branch or FlowControl.Cond_Branch)
            return operand switch
            {
                sbyte or int => FormatRelativeBranch(instruction),
                int[] offsets => "rels=[" + string.Join(
                    ", ",
                    offsets.Select(offset => RelativeTarget(instruction, offset))) + "]",
                _ => throw UnsupportedOperand(instruction, operand)
            };

        return operand switch
        {
            sbyte value => "operand=" + Invariant(value),
            byte value => "operand=" + Invariant(value),
            short value => "operand=" + Invariant(value),
            ushort value => "operand=" + Invariant(value),
            int value => "operand=" + Invariant(value),
            uint value => "operand=" + Invariant(value),
            long value => "operand=" + Invariant(value),
            ulong value => "operand=" + Invariant(value),
            float value => "operand=" + value.ToString("R", CultureInfo.InvariantCulture),
            double value => "operand=" + value.ToString("R", CultureInfo.InvariantCulture),
            char value => "operand=" + Quote(value.ToString()),
            string value => "operand=" + Quote(value),
            byte[] value => "signature=0x" + Convert.ToHexString(value),
            ParameterInfo parameter =>
                $"arg={Invariant(parameter.Position)}:{parameter.Name ?? "<unnamed>"} " +
                $"type={TypeName(parameter.ParameterType)}",
            LocalVariableInfo local =>
                $"local={Invariant(local.LocalIndex)} type={TypeName(local.LocalType)}",
            MethodBase method => "method=" + MethodName(method),
            FieldInfo field => "field=" + MemberName(field),
            Type type => "type=" + TypeName(type),
            _ => throw UnsupportedOperand(instruction, operand)
        };
    }

    private static string FormatRelativeBranch(CilInstructionInfo instruction) =>
        instruction.Instruction.Operand switch
        {
            sbyte displacement => RelativeTarget(instruction, displacement),
            int displacement => RelativeTarget(instruction, displacement),
            var operand => throw UnsupportedOperand(instruction, operand)
        };

    private static string RelativeTarget(CilInstructionInfo instruction, int displacement)
    {
        int target;
        try
        {
            target = checked(instruction.NextByteOffset + displacement);
        }
        catch (OverflowException exception)
        {
            throw new InvalidProgramException(
                $"Branch target overflows at {ByteOffset(instruction.ByteOffset)}.",
                exception);
        }

        return "rel=" + Signed(displacement) + " resolved=" + ByteOffset(target);
    }

    private static string Stack(IEnumerable<CilStackType> stack) =>
        "[" + string.Join(", ", stack.Reverse().Select(StackTypeName)) + "]";

    private static string StackTypeName(CilStackType type) =>
        type switch
        {
            CilStackType.Int32 => "i32",
            CilStackType.Int64 => "i64",
            CilStackType.Float32 => "f32",
            CilStackType.Float64 => "f64",
            CilStackType.Value value => $"value<{ShaderTypeName(value.Type)}>",
            CilStackType.ObjectReference reference => $"ref<{ShaderTypeName(reference.Type)}>",
            CilStackType.ManagedPointer pointer =>
                $"managed-ptr<{ShaderTypeName(pointer.Type.BaseType)}, {pointer.Type.AddressSpace.Kind}>",
            _ => throw new NotSupportedException(
                $"Unsupported CIL stack diagnostic variant {type.GetType().FullName}.")
        };

    private static string ShaderTypeName(IShaderType type) =>
        type switch
        {
            IPtrType pointer =>
                $"ptr<{ShaderTypeName(pointer.BaseType)}, {pointer.AddressSpace.Kind}>",
            _ => type.Name
        };

    private static string LabelList(
        IEnumerable<Label> labels,
        IReadOnlyDictionary<Label, int> labelIds) =>
        "[" + string.Join(", ", labels.Select(label => LabelName(label, labelIds))) + "]";

    private static string LabelName(Label label, IReadOnlyDictionary<Label, int> labelIds) =>
        "^" + Invariant(labelIds[label]) + "(" + (label.Name ?? "<unnamed>") + ")";

    private static string IndexRange(int start, int count) =>
        "#" + Invariant(start) + "..#" + Invariant(checked(start + count - 1));

    private static string ByteRange(int start, int end) =>
        ByteOffset(start) + ".." + ByteOffset(end);

    private static string ByteOffset(int offset) =>
        "IL_" + offset.ToString("X4", CultureInfo.InvariantCulture);

    private static string OpCodeName(CilInstructionInfo instruction) =>
        instruction.Instruction.OpCode.Name
        ?? throw new InvalidOperationException(
            $"CIL opcode at {ByteOffset(instruction.ByteOffset)} has no name.");

    private static string MethodName(MethodBase method) =>
        TypeName(method.DeclaringType) + "." + method.Name + "(" +
        string.Join(", ", method.GetParameters().Select(parameter => TypeName(parameter.ParameterType))) + ")";

    private static string MemberName(MemberInfo member) =>
        TypeName(member.DeclaringType) + "." + member.Name;

    private static string TypeName(Type? type) =>
        type?.FullName ?? type?.Name ?? "<no-declaring-type>";

    private static string Quote(string value) =>
        "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal)
                     .Replace("\"", "\\\"", StringComparison.Ordinal)
                     .Replace("\r", "\\r", StringComparison.Ordinal)
                     .Replace("\n", "\\n", StringComparison.Ordinal)
                     .Replace("\t", "\\t", StringComparison.Ordinal) + "\"";

    private static string Signed(int value) =>
        value >= 0 ? "+" + Invariant(value) : Invariant(value);

    private static string Invariant<T>(T value) where T : IFormattable =>
        value.ToString(null, CultureInfo.InvariantCulture);

    private static NotSupportedException UnsupportedOperand(
        CilInstructionInfo instruction,
        object? operand) =>
        new(
            $"Unsupported CIL diagnostic operand {operand?.GetType().FullName ?? "<null>"} " +
            $"at {ByteOffset(instruction.ByteOffset)} ({OpCodeName(instruction)}).");

    private static void WriteValues(
        IEnumerable<IShaderValue> values,
        ILocalDeclarationContext context,
        IndentedTextWriter writer)
    {
        var separator = "";
        foreach (var value in values)
        {
            writer.Write(separator);
            WriteValue(value, context, writer);
            separator = ", ";
        }
    }

    private static void PrintValueBlock(
        CilValueBasicBlock block,
        ILocalDeclarationContext context,
        IndentedTextWriter writer)
    {
        PrintValueBlockHeader(block, context, writer);
        writer.WriteLine();
        PrintValueBlockBody(block, context, writer);
    }

    private static void PrintValueBlockHeader(
        CilValueBasicBlock block,
        ILocalDeclarationContext context,
        IndentedTextWriter writer)
    {
        block.Label.Dump(context, writer);
        writer.Write(" parameters=[");
        WriteValues(block.Parameters, context, writer);
        writer.Write("]");
    }

    private static void PrintValueBlockBody(
        CilValueBasicBlock block,
        ILocalDeclarationContext context,
        IndentedTextWriter writer)
    {
        using (writer.IndentedScope())
        {
            foreach (var instruction in block.Body.Elements)
            {
                if (instruction.Result is { } result)
                {
                    result.Dump(context, writer);
                    writer.Write(" = ");
                }

                writer.Write(instruction.Operation.Name);
                writer.Write("(");
                WriteValues(instruction.Operands, context, writer);
                writer.WriteLine(")");
            }

            writer.Write("control: ");
            writer.WriteLine(block.Body.Last.Evaluate(new ValueTerminatorFormatter(context)));
        }
    }

    private static void WriteOptionalLabel(
        Label? label,
        ILocalDeclarationContext context,
        IndentedTextWriter writer)
    {
        if (label is null)
            writer.Write("none");
        else
            label.Dump(context, writer);
    }

    private static void WriteValue(
        IShaderValue value,
        ILocalDeclarationContext context,
        IndentedTextWriter writer)
    {
        switch (value)
        {
            case IntermediateValue:
                value.Dump(context, writer);
                return;
            case LiteralValue literal:
                literal.Value.PrettyPrint(writer, PrettyPrintOption.Default);
                return;
            case ParameterPointerValue parameter:
                writer.Write("&arg(");
                writer.Write(parameter.Declaration.Name);
                writer.Write(")");
                return;
            case VariablePointerValue variable:
                writer.Write("&var(");
                writer.Write(variable.Declaration.Name);
                writer.Write(")");
                return;
            case StoragePointerValue storage:
                writer.Write("&storage(");
                writer.Write(storage.VariableDeclaration.Name);
                writer.Write(")");
                return;
            case FunctionDeclaration function:
                writer.Write("@fn(");
                writer.Write(function.Name);
                writer.Write(")");
                return;
            default:
                writer.Write("<");
                writer.Write(value.Type.Name);
                writer.Write(">");
                return;
        }
    }

    private sealed class ValueTerminatorFormatter(ILocalDeclarationContext context)
        : ITerminatorSemantic<RegionJump<IShaderValue>, IShaderValue, string>
    {
        public string ReturnVoid() => "return";

        public string ReturnExpr(IShaderValue expr) => $"return {Value(expr)}";

        public string Br(RegionJump<IShaderValue> target) =>
            $"br {Jump(target)}";

        public string BrIf(
            IShaderValue condition,
            RegionJump<IShaderValue> trueTarget,
            RegionJump<IShaderValue> falseTarget) =>
            $"br_if {Value(condition)} true={Jump(trueTarget)} false={Jump(falseTarget)}";

        private string Jump(RegionJump<IShaderValue> jump) =>
            $"{Label(jump.Label)}({string.Join(", ", jump.Arguments.Select(Value))})";

        private string Label(Label label) => $"^{context.LabelIndex(label)}({label.Name})";

        private string Value(IShaderValue value)
        {
            using var text = new StringWriter(CultureInfo.InvariantCulture);
            using var valueWriter = new IndentedTextWriter(text);
            WriteValue(value, context, valueWriter);
            return text.ToString();
        }
    }
}

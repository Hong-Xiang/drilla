using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.CodeTextWriter;
using FlowControl = System.Reflection.Emit.FlowControl;

namespace DualDrill.CLSL.Frontend;

internal static class CilStagePrettyPrinter
{
    public static void PrintRawLinearCode(
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

    public static void PrintPreAnnotatedLinearCode(
        LinearCode<Annotated<CilInstructionInfo, PreStack>> code,
        IndentedTextWriter writer,
        PrettyPrintOption option)
    {
        writer.WriteLine("linear-cil pre-annotated reachable (byte ranges are half-open; stack order: bottom -> top)");
        foreach (var instruction in code.Instructions)
            instruction.PrettyPrint(
                writer,
                option,
                PrintInstruction,
                static (pre, output, _) =>
                {
                    output.Write(" pre=");
                    output.Write(Stack(pre.Types));
                    output.WriteLine();
                });
    }

    public static void PrintControlFlowGraph(
        ControlFlowGraph<CilInstructionBlock> graph,
        IndentedTextWriter writer,
        PrettyPrintOption option)
    {
        var labels = graph.Labels()
                          .OrderBy(label => graph[label].InstructionIndex)
                          .ToImmutableArray();
        var labelIds = labels.Select((label, index) => (label, index))
                             .ToDictionary(item => item.label, item => item.index);

        writer.WriteLine("reachable-cil-cfg (byte ranges are half-open; stack order: bottom -> top)");
        foreach (var label in labels)
        {
            var block = graph[label];
            var last = block.Instructions[^1].Node;
            writer.Write(LabelName(label, labelIds));
            writer.Write(" instructions=");
            writer.Write(IndexRange(block.InstructionIndex, block.InstructionCount));
            writer.Write(" bytes=");
            writer.Write(ByteRange(block.ByteOffset, last.NextByteOffset));
            writer.Write(" entry=");
            writer.Write(Stack(block.EntryStack.Types));
            writer.Write(" predecessors=");
            writer.Write(LabelList(
                graph.Predecessor(label).OrderBy(predecessor => graph[predecessor].InstructionIndex),
                labelIds));
            writer.WriteLine();
            using (writer.IndentedScope())
            {
                writer.Write("control: ");
                PrintControl(block.Terminator, labelIds, writer);
                writer.WriteLine();
            }
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
}

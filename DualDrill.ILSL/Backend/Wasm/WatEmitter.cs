using System.Globalization;
using System.Text;

namespace DualDrill.CLSL.Backend.Wasm;

public static class WatEmitter
{
    public static string Emit(WasmFunctionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.ParameterCount < 0 || plan.LocalCount < 0)
            throw new ArgumentOutOfRangeException(nameof(plan), "WASM local counts cannot be negative.");

        var writer = new Writer();
        writer.Line("(module");
        writer.Indent++;
        writer.Line(FunctionHeader(plan));
        writer.Indent++;
        writer.Write(plan.Instructions);
        writer.Indent--;
        writer.Line(")");
        writer.Indent--;
        writer.Line(")");
        return writer.ToString();
    }

    private static string FunctionHeader(WasmFunctionPlan plan)
    {
        var parameters = string.Concat(Enumerable.Repeat(" (param i32)", plan.ParameterCount));
        var locals = string.Concat(Enumerable.Repeat(" (local i32)", plan.LocalCount));
        return $"(func (export \"run\"){parameters} (result i32){locals}";
    }

    private sealed class Writer
    {
        private readonly StringBuilder text = new();

        internal int Indent { get; set; }

        internal void Write(IEnumerable<WasmInstruction> instructions)
        {
            foreach (var instruction in instructions)
                Write(instruction);
        }

        internal void Line(string value)
        {
            text.Append(' ', Indent * 2);
            text.Append(value);
            text.Append('\n');
        }

        public override string ToString() => text.ToString();

        private void Write(WasmInstruction instruction)
        {
            switch (instruction)
            {
                case WasmInstruction.I32Const constant:
                    Line($"i32.const {constant.Value.ToString(CultureInfo.InvariantCulture)}");
                    break;
                case WasmInstruction.LocalGet local:
                    Line($"local.get {local.Index.ToString(CultureInfo.InvariantCulture)}");
                    break;
                case WasmInstruction.LocalSet local:
                    Line($"local.set {local.Index.ToString(CultureInfo.InvariantCulture)}");
                    break;
                case WasmInstruction.I32Add:
                    Line("i32.add");
                    break;
                case WasmInstruction.I32Eq:
                    Line("i32.eq");
                    break;
                case WasmInstruction.I32Ne:
                    Line("i32.ne");
                    break;
                case WasmInstruction.I32LtS:
                    Line("i32.lt_s");
                    break;
                case WasmInstruction.I32LeS:
                    Line("i32.le_s");
                    break;
                case WasmInstruction.I32GtS:
                    Line("i32.gt_s");
                    break;
                case WasmInstruction.I32GeS:
                    Line("i32.ge_s");
                    break;
                case WasmInstruction.Loop loop:
                    Line("loop");
                    Indent++;
                    Write(loop.Body);
                    Indent--;
                    Line("end");
                    break;
                case WasmInstruction.If conditional:
                    Line("if");
                    Indent++;
                    Write(conditional.Then);
                    Indent--;
                    if (!conditional.Else.IsEmpty)
                    {
                        Line("else");
                        Indent++;
                        Write(conditional.Else);
                        Indent--;
                    }
                    Line("end");
                    break;
                case WasmInstruction.Br branch:
                    Line($"br {branch.Depth.ToString(CultureInfo.InvariantCulture)}");
                    break;
                case WasmInstruction.Return:
                    Line("return");
                    break;
                case WasmInstruction.Unreachable:
                    Line("unreachable");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(instruction), instruction, "Unknown WASM instruction.");
            }
        }
    }
}

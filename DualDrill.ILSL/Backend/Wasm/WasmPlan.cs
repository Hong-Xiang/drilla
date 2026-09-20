using System.Collections.Immutable;

namespace DualDrill.CLSL.Backend.Wasm;

public sealed record WasmFunctionPlan(
    int ParameterCount,
    int LocalCount,
    ImmutableArray<WasmInstruction> Instructions);

public abstract record WasmInstruction
{
    private WasmInstruction() { }

    public sealed record I32Const(int Value) : WasmInstruction;
    public sealed record LocalGet(int Index) : WasmInstruction;
    public sealed record LocalSet(int Index) : WasmInstruction;
    public sealed record I32Add : WasmInstruction;
    public sealed record I32Eq : WasmInstruction;
    public sealed record I32Ne : WasmInstruction;
    public sealed record I32LtS : WasmInstruction;
    public sealed record I32LeS : WasmInstruction;
    public sealed record I32GtS : WasmInstruction;
    public sealed record I32GeS : WasmInstruction;
    public sealed record Loop(ImmutableArray<WasmInstruction> Body) : WasmInstruction;
    public sealed record If(
        ImmutableArray<WasmInstruction> Then,
        ImmutableArray<WasmInstruction> Else) : WasmInstruction;
    public sealed record Br(int Depth) : WasmInstruction;
    public sealed record Return : WasmInstruction;
    public sealed record Unreachable : WasmInstruction;
}

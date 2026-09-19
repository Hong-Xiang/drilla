# Bounded scalar WASM CFG-dispatch prototype

This is the first executable output prototype for #116, not the complete WASM
backend. It deliberately lowers explicit CFG control to a local program-counter
dispatcher. A later native lowering will consume D's checked regions instead
of treating `RegionTree` layout metadata as control semantics.

`WasmLowering.Lower(FunctionBody4)` validates shared compiler IR and produces an
immutable backend-local `WasmFunctionPlan`. `WatEmitter.Emit(plan)` only formats
that plan as deterministic WAT exporting `run`.

The initial backend supports public `i32` parameters and one `i32` result,
internal `i32`/`bool` values, literals, loads/stores, wrapping addition, signed
comparisons, explicit boolean conversions, `nop`, branches, and expression
returns. Lowering uses a local program-counter dispatch loop. Edge arguments are
first copied to scratch locals, then assigned to target block parameters, so
self-edges and same-target conditional edges retain parallel-copy semantics.
Every referenced function local must be initialized by an unconditional store
in the entry block before any read; initialization only on later paths is
rejected conservatively.

Lowering rejects malformed SSA, invalid edges, unsupported signatures,
non-function storage, shader semantics, uninitialized locals, calls, memory,
WASI, floats, unsigned arithmetic, and other operations outside this bounded
slice. It does not inspect `RegionTree` layout metadata or route through Slang.
Calls are not supported by this prototype.

`FunctionBody4` construction traverses jump targets before the backend runs, so
an unknown target can throw from that shared constructor and never reach
`WasmLowering.Lower`. Inputs that do reach lowering receive contextual
`WasmLoweringException` diagnostics for malformed payloads, values, and types.
The public `WasmLowering.Lower(null)` boundary retains `ArgumentNullException`.

The checked-in `examples-H/wasm-{add,choose,sum}.{ir,wat,results}` files are
captured from the constructor fixtures. The `.ir` files use the existing
`FunctionBody4.Dump()` formatter with only trailing whitespace removed by the
test-local capture helper; the `.wat` files use `WatEmitter`.

Run the acceptance tests in the pinned shell:

```sh
nix develop --builders '' --command \
  dotnet test DualDrill.CLSL.Test/DualDrill.CLSL.Test.csproj \
  --no-restore --configuration Debug \
  --filter 'FullyQualifiedName~WasmBackendTests'
```

To deliberately refresh the checked-in captures after reviewing an emitter
change, prefix that command with `DRILLA_UPDATE_WASM_GOLDENS=1`.

# WASM output captures

These files are generated and checked by `WasmBackendTests`:

- `wasm-add`: wrapping `i32.add`, including signed overflow examples.
- `wasm-choose`: `i32`-to-`bool` normalization and two differently bound edges
  targeting the same block.
- `wasm-sum`: loop-carried sum/counter values through the CFG dispatcher.
  Its captured boundary inputs are `-1, 0, 1, 5, 10000`, producing
  `0, 0, 0, 10, 49995000`.

Each `.ir` is actual `RegionFunctionBody.Dump()` output with only trailing whitespace
removed by the test-local capture helper. Each `.wat` is actual
`WatEmitter.Emit()` output, and each `.results` file records values obtained by
assembling with pinned WABT 1.0.36, validating with `wasm-validate`, and running
the module independently in Node/V8. Generated `.wasm` files remain test build
artifacts rather than checked-in binaries.

# Structural Control Flow

## Recover Structural Control Flow From Dotnet CIL

The intended general reducible-CFG translation is based on
[Beyond Relooper](https://dl.acm.org/doi/10.1145/3547621). The current emitter is
not a complete implementation of that algorithm.

See [shared IR constructs and stage contracts](./ir_spec.md) and
[pass invariants](./compiler/passes.md) for the current pipeline and intended
separation between typed CFG, scoped nested region IR, and target AST.
Region containment alone does not prove structural legality, and target AST
layout is a separate obligation from identifying region owners and shared joins.

`SlangTargetLowering` now consumes the checked lexical `Forward`/`Repeat`
continuations on `RegionFunctionBody`. It places each original region once, realizes
selected-edge parameter copies, and uses explicit `SlangDoOnce` carriers plus
exact continuation gates to unwind multiple exits and outer-loop transfers.
`SlangEmitter` consumes only the resulting immutable `SlangFunctionBody`; it
prints `SlangDoOnce` and `SlangLoop` directly, and does not inspect Region graphs,
infer joins, or derive loop kind from provenance.

The public Slang/WGSL path resolves stable pointer parameters first and leaves
ordinary parameters for selected-edge lowering. Synthetic carrier nesting and
unwind work are linear in lexical depth. Checked Region construction rejects
irreducible/side-entry input; there is no node splitting, full-function program
counter, fabricated return, GPU reconvergence guarantee, or general Beyond
Relooper implementation.

## GPU Reconvergence Research

See [Maximal Reconvergence for a CIL-First Shader Compiler](./maximal-reconvergence-research.md)
for the 2026-09-14 research snapshot: normative semantics, CIL-specific design
choices, Slang and target support, proposed implementation slices, and validation
requirements. This is research for follow-up work, not an implemented guarantee.
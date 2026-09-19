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

`SlangTargetLowering` now owns the existing bounded layout policy before text
emission. It permits zero or one distinct non-terminating destination outside
an existing loop subtree; direct terminal paths retain their actions and return.
It rejects multiple distinct normal destinations rather than choosing one.
`SlangEmitter` consumes only the resulting immutable `SlangFunctionBody` and
does not inspect Region graphs or infer joins and loop ownership.

This extraction does not repair the input Region IR or establish a general
checked scoped-control invariant. General multi-exit structurization,
edge-local value lowering, and an explicit irreducible-input policy remain
follow-up work.

## GPU Reconvergence Research

See [Maximal Reconvergence for a CIL-First Shader Compiler](./maximal-reconvergence-research.md)
for the 2026-09-14 research snapshot: normative semantics, CIL-specific design
choices, Slang and target support, proposed implementation slices, and validation
requirements. This is research for follow-up work, not an implemented guarantee.
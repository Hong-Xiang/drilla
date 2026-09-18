# Compiler Passes and Stage Invariants

The [shared IR contract](../ir_spec.md) is the authoritative distinction between
implemented representations and intended stages. A logical pass boundary does
not require a new CLR type. Conversely, using one CLR type does not excuse
leaving a consumer's required invariant unspecified.

## Implemented Public Path

| Component | Input -> output | Current responsibility |
|---|---|---|
| `CilMethodDecoder` | Method metadata -> `LinearCode<CilInstructionInfo>` | Preserve every instruction, original index/byte range, and immutable method environment. |
| `CilPreStackAnalyzer` | Raw linear CIL and method symbols -> `LinearCode<Annotated<CilInstructionInfo, PreStack>>` | Validate whole-source control first, then propagate exact normalized stacks; successful output contains only reachable original positions. |
| `CilControlFlowGraphBuilder` | Raw and completed Pre-annotated linear values -> reachable `ControlFlowGraph<CilInstructionBlock>` | Filter before predecessor construction, retain concrete native control, and carry each instruction beside its Pre annotation. |
| `RuntimeReflectionParser.ParseMethodBody3` | `Annotated<ControlFlowGraph<CilInstructionBlock>, ControlFlowAnalysis>` -> `FunctionBody4` | Create block inputs from adjacent Pre facts, check every concrete instruction/edge stack, and reuse the same graph analysis for postdominance and the region tree. |
| `FunctionToOperationPass` | `FunctionBody4` -> `FunctionBody4` | Lower recognized operation/constructor calls; preserve other instructions and control references. |
| `RegionParameterToLocalVariablePass` | `FunctionBody4` -> `FunctionBody4` | Resolve supported pointer aliases and remove region parameters under existing restrictions. |
| `SlangEmitter` | `FunctionBody4` -> Slang text | Resolve supported lexical transfers, place code, and emit syntax. These responsibilities are not yet separate passes. |
| `SlangService` | Slang text -> WGSL | Invoke the external Slang compiler. |

The `IR` output option formats `FunctionBody4`; it does not produce a distinct
target AST. `IShaderModuleSimplePass` is the existing same-body-type pass
interface. There is no implemented general pass scheduler, configurable
optimization-level pipeline, or comprehensive inter-pass verifier here.

## Desired Logical Separation

```text
linear stack instructions
  -> complete linear source + sparse reachable-position Pre map
  -> reachable CFG of typed stack instructions
  -> typed CFG with block arguments
  -> scoped nested regions with shared joins and SSA-like values
  -> target-language AST
  -> source text
```

This is a staged contract, not a list of already implemented function-body
classes. Shared instructions, terminators, sequences, labels, and region
constructors can serve multiple stages. Pre analysis and reachable CFG
construction are now separate internal operations. Value lifting remains fused
with parsing, and the emitter still performs work intended for region-to-AST
lowering.

The agreed [linear CIL design](linear-cil.md) refines the frontend ordering:
preserve native predicates and original offsets, analyze Pre stack types on
instruction positions, then split into blocks and lower instructions only after
labels are stable. Generic analysis uses a narrow control view while concrete
terminator payload remains intact. This does not require splitting `TE` or
rebuilding rich CIL nodes from a lossy `Unit` projection.

### Control Structurization

Establish ownership and legal references before target syntax is chosen.
A shared join is defined once and may have multiple incoming control references.
Loop headers require dominance-backed backedges; a completed reducibility
contract must not be inferred from identifying some natural loop headers.

Forward merge analysis must preserve terminator-arm identity. A true arm and
false arm referencing the same label can still carry different arguments.
Projection to successors is useful for control analysis but cannot reconstruct
those discarded values.

### Region-to-AST Lowering

Choose lexical placement for shared joins, continuations, terminal paths, and
their value transfers. A valid scoped region is not automatically legal Slang or
WGSL syntax: exiting several scopes is not the same as emitting a nearest-loop
`break` or `continue`.

Target AST construction must preserve the dynamic occurrence and order of
original effects. It may introduce explicit local bindings; it must not expand
a shared effectful definition at every reference as if it were a pure expression.

### Value Lowering

Block parameters, SSA phi operands, mutable locals, and typed stack results are
different representations of path-selected values. Parameter elimination must
preserve selected-edge semantics and parallel copies.

This can happen before structurization, using appropriate edge-local actions,
or after region construction, using owned transfers and explicit values.
Choose the order in the slice that implements it; do not erase arguments early
and then require an emitter to recover them from destination labels.

The existing pointer restrictions remain separate from ordinary scalar copying:
a supported constant storage address is not a mutable pointer-valued local.

## Small Test Units

| Transformation | Minimal independent examples |
|---|---|
| Linear CIL -> Pre stack annotation | Jump versus physical adjacency, typed stack joins, loop propagation, and unreachable versus empty entry state. |
| Linear CIL -> CFG | A separately labeled final instruction, explicit return, conditional branch, and ordinary fallthrough. |
| Stack CFG -> typed CFG | Equal incoming stack shapes, mismatched shapes, ordered edge arguments, and normalized scalar call boundaries. |
| CFG -> scoped regions | A diamond, loop header, shared join, nested continuation, and illegal cross-scope reference. |
| Region -> AST | Shared effectful tail, early return, nested exit, and multiple references to one continuation. |
| Parameter/value lowering | Two arms with different values to one target, cyclic parallel copies, and stable versus ambiguous pointer roots. |
| AST -> text | Precedence, declarations/scopes, control syntax, and target compiler acceptance. |

For shared generic constructors, test identity/composition laws using semantic
contents rather than incidental object-storage equality. For control/value maps,
preserve labels and ordered arm/argument references unless the pass explicitly
owns their transformation.

Existing bounded CPU/CFG/emitted-source comparisons remain the end-to-end
guardrail; these local units do not replace them. The proposed scope and AST
units become required as those stages are implemented, not evidence that they
already exist.

## Analysis Lifetime and Failure

State each pass's preconditions, established postconditions, and invalidated
analyses. Control-edge rewrites cannot silently retain dominance or join-layout
results from an older graph. A same-type pass may preserve an invariant, establish
a new one, or invalidate one; its contract must say which.

An unavailable analysis is not the same as a completed analysis with no result.
Keep raw representation, analysis results, and checked consumer requirements
distinct. Type markers must be backed by controlled construction or validation.

Failures must remain explicit. Unsupported control graphs, edge arguments,
operations, and pointer merges are not repaired by dropping edges, fabricating
returns, or substituting empty values. See the
[current limitations](../ir_spec.md#current-lowering-limits) before relying on a
planned invariant.

# Compiler Passes and Stage Invariants

The [shared IR contract](../ir_spec.md) is the authoritative distinction between
implemented representations and intended stages. A logical pass boundary does
not require a new CLR type. Conversely, using one CLR type does not excuse
leaving a consumer's required invariant unspecified.

## Implemented Boundary: Parse a Complete CIL Module

The parser/collector must finish after collecting the module's declarations,
referenced types and original CIL bodies. It must not perform Pre stack analysis,
construct a BB CFG, lift stack values, compute dominance, or create region IR.
Subsequent transformations are explicit pass functions orchestrated outside the
parser, each with one semantic responsibility.

### Deliberate Collection-Policy Change

Module membership is based on all references in the original CIL, not on
instruction reachability. Recursively collect referenced methods and their
referenced types/bodies until an explicitly mapped builtin type/method or
intrinsic declaration boundary is reached. Include references in syntactically
dead instruction positions. Declaration-first cycle handling supports repeated
references and mutual recursion without executing the methods.

Collect types required by signatures, locals, fields, construction and other
supported metadata operands. This means the reference closure, not enumerating
every unrelated method in an assembly. A builtin/intrinsic boundary is explicit;
an unfamiliar method is not silently treated as a builtin just because parsing
its body is inconvenient.

This supersedes the previous "dead callees are not declared or compiled" rule.
Collection is allowed to do additional work on dead references. A non-builtin
referenced method with unsupported content may now cause later compilation to
fail even when the call site is unreachable. Record such cases as an intentional
policy change rather than changing tests silently.

Parsing collects raw code and metadata; semantic support checks belong to the
appropriate later pass where possible. Do not drop references to `throw` or
exception-handling code to make collection succeed, and do not claim support
for those semantics. If collection cannot decode or resolve a reference, report
the source method/operand explicitly instead of publishing a partial module.

### Boundary Audit Clarifications

Every type-bearing root enters the same recursive closure operation: entry and
referenced method signatures, locals, instruction operands, field owners/types,
module variables, and static method declaring types. Layout closure includes
base types and inherited instance fields until an explicit builtin type
boundary. Function-pointer signatures contribute their return and parameter
types, then stop as a non-layout metadata boundary; this does not imply `calli`
or function-pointer backend support. Type visitation is marked before walking
members so reference cycles terminate. A CLR value-type shape that cannot be
represented without recursive construction fails with type context rather than
overflowing the parser stack. Collection still does not enumerate unrelated
assembly methods.

Attributed shader module properties are not a supported variable representation.
The existing CIL frontend resolves fields, while a property access is a getter
call and has no established variable/getter ABI. Parsing therefore rejects an
attributed module property explicitly instead of publishing a declaration that
later Pre cannot resolve. Attributed fields remain the supported module-variable
form.

The frozen symbol view snapshots the complete parent chain. A returned raw body
must not observe later mutation through a parent `CompilationContext`; only the
known immutable shared-builtin table or an already frozen table may remain as a
fallback.

Every public metadata-collection entry point is fail-closed, including
`ParseType`, `ParseField`, `ParseParameter`, `ParseStaticField`, module-variable
and entry discovery, and method traversal. Recursive internal helpers stay
inside the active operation rather than starting nested transactions. Any
collection failure poisons that parser instance even when no root method was
identified, and no later call may return cached placeholders or publish
declarations accumulated by the failed attempt. Contextual wrapping is limited
to expected reflection/metadata failures; programmer faults and fatal runtime
exceptions are not reclassified as unsupported CIL.

### Pass Responsibilities

```text
parse/collect the all-reference CIL module
  -> Pre stack analysis over each collected function
  -> labelled block partitioning using completed Pre facts
  -> CIL-to-shader-stack instruction/type lowering
  -> generic CFG construction over shader-stack blocks
  -> stack-to-explicit-value lowering
  -> eligible private-local promotion
  -> control-flow analysis and region construction
  -> existing operation/parameter lowering
  -> target emission
```

Moving code into passes must change the actual public producer/consumer path,
not leave `ParseShaderModule` returning already-lowered `RegionFunctionBody` while
renaming its private helpers. Reuse the module/body generics where appropriate
and document any necessary public API migration without compatibility shims.

The later Pre pass may still omit unreachable positions inside a collected
method's CFG; that is independent of which method bodies were collected into
the module. Preserve complete source, native predicates, original labels and
offsets, pointer constraints, exact type joins, and explicit unsupported
operation failures.

Pass-local scratch dictionaries and worklists are allowed. Published IR stage
values remain immutable and printable. A pass explicitly preserves, drops or
recomputes annotations; there is no automatic invalidation framework.

### Implemented BB-Local Analysis Facts

Where a fact is about one BB, prefer a result shaped like
`CFG<Annotated<TBasicBlock, TBlockFacts>>`. Immediate dominance, postdominance,
loop-header and relevant ordering facts can reference other blocks by stable
labels. Their global computation does not require a permanent global
BB-to-facts dictionary as the consumer API.

Choose one authoritative representation, deriving auxiliary indexes as needed;
do not publish duplicate mutable copies of the same facts. A topology-changing
pass may invalidate annotations on multiple BBs and can return a globally
rebuilt immutable result. General incremental maintenance is not required.

This analysis-result reorganization is a separate control-facts pass after the
parser/pass boundary. It reuses the existing algorithms and does not introduce a
new structurization algorithm inside collection or region construction.

The implemented `BlockControlFacts` fields have deliberately narrow meanings.
Analysis requires every definition to be entry-reachable.
`ReversePostOrderIndex` is the existing DFS reverse-postorder number and
`ImmediateDominator` is a stable original label, or `null` for the entry.
`PostDominance` distinguishes a real `Block`, the analysis-only `FunctionExit`,
and `NoExitPath`; every alternative also exposes structural `MayDiverge`.
`IncomingArms` preserves every arm, ordered by source reverse-postorder then
successor ordinal, including parallel arms to one target. An arm is a backedge
exactly when its target dominates its source; forward means only “not a
backedge,” not increasing reverse-postorder. `IsLoopHeader` is derived from the
presence of an incoming backedge. These facts do not claim reducibility,
natural-loop membership, reconvergence, or general structurization.

## Implemented Public Path

| Component | Input -> output | Current responsibility |
|---|---|---|
| `RuntimeReflectionParser` | Reflection roots -> `ShaderModuleDeclaration<RawCilFunctionBody>` | Collect declarations, immutable symbol metadata, and every referenced non-boundary original CIL body, including references at unreachable instruction positions. |
| `CilMethodDecoder` | Method metadata -> `LinearCode<CilInstructionInfo>` | Preserve every instruction, original index/byte range, method body and immutable method environment without semantic lowering. |
| `CilPreStackPass` | Raw CIL module -> `ShaderModuleDeclaration<PreCilFunctionBody>` | Reject unsupported EH, validate whole-source control, and propagate exact normalized stacks; successful per-function output contains only reachable original positions. |
| `CilBlockPartitionPass` | Pre module -> `ShaderModuleDeclaration<LabelledCilFunctionBody>` | Partition the complete reachable Pre source into `BlockList<CilInstructionBlock>` while retaining original instruction and annotation identity. |
| `CilToShaderStackPass` | Labelled CIL module -> `ShaderModuleDeclaration<ShaderStackFunctionBody>` | Lower each CIL instruction to explicit typed stack operations, aliases and drops with derived Pre/Post transitions and numeric provenance. |
| `ShaderStackControlFlowPass` | Shader-stack block module -> `ShaderModuleDeclaration<ShaderStackControlFlowBody>` | Validate exact edge stack equality and construct the first frontend CFG through the generic factory. |
| `ShaderStackToValuePass` | Shader-stack CFG module -> `ShaderModuleDeclaration<CilValueControlFlowBody>` | Mechanically read depths, pop/push the declared transition, preserve provenance in instruction payloads, and produce ordered block arguments. |
| `CilLocalPromotionPass` | Flat value CFG module -> `ShaderModuleDeclaration<CilValueControlFlowBody>` | Promote definitely assigned, direct nonescaping function-local `i32` and `bool` loads/stores through the existing SSA transform, using the raw method's exact locals and `InitLocals` metadata. Escaped, unsupported and incompletely initialized locals remain in storage. |
| `CilBlockControlFactsPass` | Promoted value CFG module -> `ShaderModuleDeclaration<CilValueControlFactsBody>` | Compute reverse-postorder, immediate dominators, finite-exit postdominance with explicit function-exit/no-exit results, structural may-diverge, and ordered incoming-arm/backedge facts once, publishing them as `ControlFlowGraph<Annotated<CilValueBasicBlock, BlockControlFacts>>`; loop-header status is derived from incoming backedges. |
| `CilRegionPass` | BB-annotated value CFG module -> `ShaderModuleDeclaration<RegionFunctionBody>` | Consume local control facts as authoritative input, derive a temporary immediate-dominator child index, preserve descending-RPO region-child order, and construct the existing region tree without rerunning control-flow analysis. |
| `FunctionToOperationPass` | `RegionFunctionBody` -> `RegionFunctionBody` | Lower recognized operation/constructor calls; preserve other instructions and control references. |
| `StablePointerRegionParameterPass` | `RegionFunctionBody` -> `RegionFunctionBody` | Resolve supported pointer aliases while preserving ordinary parameters and ordered arm arguments. |
| `SlangTargetLowering` | `RegionFunctionBody` -> `SlangFunctionBody` | Consume checked scoped transfers, place each original body once, snapshot selected-arm values before writes, and materialize typed captures plus explicit one-shot/repeat control. |
| `RegionParameterToLocalVariablePass` | `RegionFunctionBody` -> `RegionFunctionBody` | Legacy non-target erasure of ordinary parameters after applying the shared stable-pointer policy. |
| `SlangEmitter` | `SlangFunctionBody` -> Slang text | Format the target AST without Region traversal, postdominance queries, or control-layout inference. |
| `SlangService` | Slang text -> WGSL | Invoke the external Slang compiler. |

The `IR` output option formats `RegionFunctionBody`. Public WGSL output rejects any
original `NoExitPath` block before operation, pointer, or target lowering; IR and
Slang remain available for those modules. `IShaderModuleSimplePass` is the
existing same-body-type pass interface. There is no implemented general pass
scheduler, configurable optimization-level pipeline, or comprehensive
inter-pass verifier here.

## Desired Logical Separation

```text
raw CIL LinearCode
  -> strict CIL Pre stack analysis -> typed CIL LinearCode
  -> basic-block partitioning / stable label binding -> labelled CIL BlockList
  -> CIL instruction/type lowering -> labelled shader stack BlockList
  -> generic CFG construction -> shader stack CFG
  -> stack-to-explicit-values -> shader value CFG with block arguments
  -> direct scalar local promotion -> shader value CFG with appended block arguments
  -> scoped nested regions with shared joins and SSA-like values
  -> target-language AST
  -> source text
```

Issue #114's frontend slices implement the block-list/CFG separation and early
shader-stack lowering. `InstructionBlockPartitioner` binds labels and
original ranges; immutable `BlockList<TBlock>` validates definitions and targets
without graph indexes. `ControlFlowGraph.Create` consumes the stored list and an
explicit pure control projection, including disconnected definitions and ordered
duplicate arms; it has no CIL opcode or source-index semantics.

There is no public CIL CFG. The generic CFG is constructed only after CIL has
become shader-stack IR. Shader operands are typed stack depths or resolved
literals/function/stable-address symbols; they never contain intermediate
values. Every expansion and terminator records its own transition and source
provenance. See the [actual diagnostics](linear-cil.md#read-only-stage-diagnostics).

Shared instructions, terminators, sequences, labels, and region constructors
serve multiple stages. Parsing, Pre analysis, reachable CFG construction, flat
value lifting, local promotion, BB-local control facts, and region construction now have distinct
typed producer/consumer boundaries. Changing the entry, block set, successor
destination/order, or arm multiplicity requires rerunning
`CilBlockControlFactsPass`; a payload-only mapping may preserve facts only when
topology and ordered arm identity are unchanged. There is no incremental cache or invalidation manager. Target layout is performed
once by `SlangTargetLowering`; the emitter is syntax-only.

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
| Typed linear CIL -> labelled BlockList | Original ranges across gaps, stable labels before payload construction, final return/branch, invalid final conditional, and ordinary fallthrough that cannot skip a gap. |
| BlockList -> generic CFG | Non-first entry, disconnected stored definitions, same-name distinct labels, payload/control identity, and ordered duplicate arms. |
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

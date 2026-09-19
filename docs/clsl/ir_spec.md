# Shared IR Constructs and Stage Contracts

This is the canonical contract for organizing CLSL's intermediate
representations. It distinguishes implemented representations from intended
stage invariants. An invariant listed here is not a claim that a corresponding
validator or transformation already exists.

The design uses a small set of generic constructors, not a separate class
hierarchy for every pass. An optimization or lowering may legitimately return
the same CLR type it consumes. Nested region IR and target-language AST remain
different logical stages, even when they reuse constructors.

## Implemented Refinement: Immutable Annotated Stage Values

The runtime-reflection frontend now uses one shared
`Annotated<TNode, TAnnotation>` constructor.

Use annotation at the scope of the fact:

```text
LinearCode<CilInstructionInfo>
LinearCode<Annotated<CilInstructionInfo, PreStack>>
CFG<Annotated<TBasicBlock, DominanceNodeInfo>>
Annotated<CFG<TBasicBlock>, DominatorTree>
```

These are type-shape examples, not a requirement to materialize all four forms
or duplicate the complete dominance result per block. A graph-wide analysis
should have one authoritative result; per-node projections can be derived when
needed. `Annotated` contains `Node` and `Annotation`, not an analysis-in-progress
flag or proof of semantic correctness.

Raw linear CIL and completed Pre-annotated linear CIL must be different
immutable values, not nullable fields gradually populated on the same model.
The reachable annotated sequence pairs each instruction with its entry stack.
It preserves original instruction identities, offsets and order; its compact
array index is not a replacement for the original instruction index. The
complete raw source remains available for diagnostics, including dead code.
Both stages may share the immutable instruction data.

The worklist may still use an index-to-stack dictionary internally. On success
it produces a completed stage value consumed by CFG construction. No pending
state is published as an empty stack; failures do not publish a partial stage.
Method signatures, local declarations and source metadata are a shared immutable
environment, not a reason to keep raw/Pre/CFG completion caches in one object.
Mutable parser symbol/recursion/failure bookkeeping stays separate from the IR.

Each real stage has a fixed, read-only `PrettyPrint` operation using the existing
`IPrintable` contract. The receiver's type determines the printed representation;
it must not change output stages according to mutable availability flags or
implicitly run analysis. Reuse the current stage formatting instead of adding
another printer registry. A small annotation constructor is preferred to
`ValueTuple` so the owned type can participate in typed printing composition.
Do not create wrappers whose only purpose is renaming `DumpXXX` calls while
leaving the old nullable-stage model authoritative.

Generic annotations do not automatically inherit a wrapped BB's control
interface. Topology consumers must obtain the original node's control projection
through explicit composition; predicate/edge data remain on the concrete node.
Do not duplicate an editable successor or special-case each annotation payload
inside CFG analysis.

Analysis validity belongs to the pass contract. A pure `Select` maps exactly the
requested fields; it does not secretly discard facts or claim an arbitrary node
rewrite preserves them. A rewriting pass explicitly preserves, replaces, or
drops the annotation and recomputes facts as needed. Type distinctions can
prevent passing raw code where completed facts are required, but do not prove
arbitrary annotations or transformations semantically valid.

`Annotated.Select`, `SelectNode`, and `SelectAnnotation` require a fixed printer
for their output types. Mapping laws apply to the immutable `Node` and
`Annotation` data; a type-changing map never carries an incompatible printer
from its input.

The implementation wires these values through a raw-module collector and
explicit Pre, CFG, flat-value, BB-control-facts and region passes. Existing
`ControlFlowAnalysis` algorithms are internal to the control-facts producer;
their RPO, immediate-dominator, immediate-postdominator and loop-header results
are published locally as `CFG<Annotated<TBlock, BlockControlFacts>>`. The region
consumer reads those facts instead of recomputing or retaining the global
analysis object. Superseded parser-owned completion caches were removed, and the source/API
migration is documented in [linear-cil.md](compiler/linear-cil.md). Region/AST
algorithms and generic invalidation frameworks remain outside this refinement.

## Three Relations, Not One Tree

- **Containment:** which expression or region owns a definition.
- **Control reference:** which labeled block or continuation receives control.
- **Value reference:** which definition supplies an instruction operand or edge
  argument.

A region's containment can form a tree while multiple control references share
one block definition. Traversing definitions is not executing them. Turning that
representation into a target AST must arrange the shared definition without
accidentally repeating, skipping, or moving its effects.

## Shared Constructors

| Construct | Responsibility | What it does not establish |
|---|---|---|
| `Seq<TElement, TLast>` | A recursive sequence with a distinct final element; a basic block can use instructions followed by one terminator. | Valid instruction semantics or validity of a default-constructed wrapper. |
| `Instruction<TOperand, TResult>` | An operation with explicit operand and result representations. | Operand/type compatibility merely from the generic parameters. |
| `ITerminator<TTarget, TValue>` | Return, unconditional branch, or two ordered conditional arms. | Target binding, availability of values, or legal lexical scope. |
| `RegionJump<TValue>` | One concrete `Label` plus an immutable, ordered argument payload. | Destination membership, parameter arity/types, or permission to enter a region. |
| `RegionTree<TLabel, TBody>` | Nested block/loop bindings and a body, with reusable mapping and folding. | A target AST, execution order, or proof that every reference is structurally legal. |
| `ShaderModuleDeclaration<TBody>` | Shared declarations with a chosen function-body representation. | A mandatory different body type for every transformation. |

Reuse the existing semantic/fold interfaces when interpreting these constructors.
Do not introduce an unconstrained universal node-with-children representation
that loses the difference between operands, branch arms, bindings, and executable
sequences. The same recursion machinery need not imply identical node grammars.

### Value-Carrying Jumps

`RegionJump<TValue>.Select` changes only the argument representation. For an
initialized argument array it preserves the exact `Label` instance, arity,
argument order, and duplicates. It applies the mapper once per argument in order;
mapper failures propagate rather than becoming an empty or partially successful
jump.

This is a payload map, not a graph rewrite or a binder. `FunctionBody4.MapValueUse`
uses it for existing value-use rewriting. Parameter removal changes arity and
therefore remains an explicit reconstruction, not a payload map.

Raw constructors are not checked function boundaries. In particular, generic
signatures do not prevent all null runtime values or a default `ImmutableArray`,
and do not prove that an argument has the target parameter's shader type.

### Control Projection Is Lossy

`ToSuccessor` projects a terminator to its control successors. It discards return
values, branch conditions, and jump arguments. It preserves
termination/unconditional/conditional control shape, target identity, true/false
ordering, and two conditional arms even when they refer to the same label.
Both value-return and void-return terminators project to termination.

It is suitable for graph analysis, not for reconstructing edge-value semantics.
Neither this projection nor a set of successor labels may silently equate
`join(a)` with `join(b)`.

## Actual Pipeline

The current public compiler path is:

```text
C# compiled by .NET
  -> RuntimeReflectionParser
  -> `ShaderModuleDeclaration<RawCilFunctionBody>`
  -> CilPreStackPass
  -> `ShaderModuleDeclaration<PreCilFunctionBody>`
  -> CilControlFlowPass
  -> `ShaderModuleDeclaration<MethodBodyAnalysisModel>`
  -> CilStackToValuePass
  -> `ShaderModuleDeclaration<CilValueControlFlowBody>`
  -> CilBlockControlFactsPass
  -> `ShaderModuleDeclaration<CilValueControlFactsBody>`
  -> CilRegionPass
  -> `ShaderModuleDeclaration<FunctionBody4>`
  -> FunctionToOperationPass                  : FunctionBody4 -> FunctionBody4
  -> RegionParameterToLocalVariablePass       : FunctionBody4 -> FunctionBody4
  -> SlangTargetLowering
  -> `ShaderModuleDeclaration<SlangFunctionBody>`
  -> SlangEmitter
  -> Slang source
  -> slangc                                  : Slang source -> WGSL
```

`RuntimeReflectionParser` stops at a complete raw CIL module. Module membership
follows all original-CIL references, including dead instruction positions, up to
explicit shared-builtin, operation-attribute and mapped-vector/member boundaries.
The later Pre pass still filters unreachable positions inside each collected
function. `FunctionBody4` combines typed instructions and parameterized
terminators with a `RegionTree` built by the final frontend pass.
`SlangTargetLowering` validates the current argument-free input contract and
performs the supported lexical layout before the syntax-only emitter runs.
There is not yet an independent general scoped-region validator or complete
structurization pass.

`ExprValue`/`ExprTree` and the `AbstractSyntaxTree` directory do not constitute a
complete AST function-body stage in this pipeline. Older design examples,
experimental backends, and the identity `CommonOperationLoweringPass` must not
be presented as additional active compilation stages.

## Logical Stages and Their Obligations

These boundaries split reasoning and testing; they do not require unrelated
IR implementations for each pass. The existing region binding tree is not a
claim of complete scoped-control legality; the implemented target lowering
accepts only its documented subset.

| Stage | Required invariant | Current owner or implementation boundary |
|---|---|---|
| Raw CIL module | All declarations and supported metadata references reachable from the roots through original CIL are present; each non-boundary method body is losslessly decoded once. | `RuntimeReflectionParser` produces `ShaderModuleDeclaration<RawCilFunctionBody>` with frozen symbol views. |
| Linear Pre facts | Reachable entries have exact normalized stack types; absence from the completed value means unreachable. | `CilPreStackPass` produces `ShaderModuleDeclaration<PreCilFunctionBody>`; full original source remains separate. |
| CFG of CIL blocks | Reachable instruction ranges are partitioned correctly; explicit terminators and legitimate fallthrough edges are preserved, without dead predecessors. | `CilControlFlowPass` produces `ShaderModuleDeclaration<MethodBodyAnalysisModel>`. |
| Typed CFG with block arguments | Each block has one terminator; edge arity/types agree with destination parameters; values are available on the selected path. | `CilStackToValuePass` produces a flat `ControlFlowGraph<CilValueBasicBlock>`. Validation is partial, not a complete verifier. |
| BB-annotated value CFG | Original blocks, labels and ordered edges remain unchanged; local facts hold existing RPO, IDom, IPDom and loop-header results. | `CilBlockControlFactsPass` publishes `ControlFlowGraph<Annotated<CilValueBasicBlock, BlockControlFacts>>`. |
| Region binding tree | Consume published local facts and preserve descending-RPO dominator-child order without reanalysis. | `CilRegionPass` produces `FunctionBody4`; general structurization and scoped-join legality remain later work. |
| Operation/value lowering | The transformation preserves control identities and effects while establishing its declared operation or parameter postcondition. | Existing same-type passes; their current restrictions are described below. |
| Target AST | Executable control has a target-language realization with explicit lexical placement; effects retain their order and dynamic multiplicity; address aliases are typed places. | `SlangTargetLowering` produces `ShaderModuleDeclaration<SlangFunctionBody>` for the currently supported Region subset. |

The typed CFG is SSA-like, not a claim of whole-program SSA: explicit loads,
stores, and mutable local storage coexist with intermediate values and block
parameters.

The [linear CIL frontend contract](compiler/linear-cil.md) describes the now
implemented Pre-before-CFG ordering and exact supported type/merge rules. It
retains full source plus a sparse completed Pre map rather than propagating an
unreachable-state variant downstream. Native CIL predicates and concrete
terminator payload remain behind narrow generic control views; `TE` is not split
merely to expose data unused by topology analysis. Instruction-changing lowering
follows stable CFG label construction. Independent value lifting and scoped
region/AST stages remain later work.

Structurization and block-parameter elimination are distinct transformations.
Keeping parameters through a scoped region stage is valid. Eliminating them
earlier is also valid if copies are attached to the selected edges, with
parallel-copy semantics. The architecture does not prescribe an order merely
because both passes can consume the same representation.

## Analysis Results and Phase State

An absent immediate postdominator can be a legitimate result of a completed
analysis; it must not also mean that analysis has not run. Nor is an immediate
postdominator interchangeable with a join binding or lexical continuation.

For future checked stage boundaries:

- Associate analysis with the exact graph it describes. Changing control edges
  invalidates analyses that depend on them.
- Separate "analysis not available" from a completed result containing no
  continuation. A completed optional result is not itself a design defect.
- Establish scope/type obligations through controlled construction or explicit
  validation. A marker type by itself is not evidence that validation happened.
- Use another generic instantiation or a thin checked wrapper only where it
  communicates a real consumer requirement; do not add a marker for every pass.

Current `RegionTree<TLabel, TBody>` uses unconstrained `TLabel?` and `default` for
continuations. This is not a sound general option encoding for arbitrary
value-type labels. Current compiler use is with concrete reference-type `Label`;
fixing the generic absence representation is a separate step.

## Current Lowering Limits

`RegionParameterToLocalVariablePass` resolves only supported stable pointer
aliases. It rejects different arguments on conditional arms sharing one target.
For distinct targets, its current value stores are inserted before the branch,
not represented as general edge-local actions. Removing the arguments does not
prove that a general edge-value lowering has been implemented.

During Slang target lowering, the current implementation supports a lexical
loop with zero or one distinct non-terminating destination outside its existing
region subtree. Direct terminal targets retain their actions and return in the
selected arm. Multiple distinct normal destinations, residual region
parameters/arguments, missing labels, unowned expansion cycles, and repeated
effectful nonterminal placements outside supported loop-transfer continuations
are explicitly rejected. The pass preserves the current parameter pass's store
order; it does not recover selected-edge ownership already erased there. It is
not the complete Beyond Relooper algorithm or a general irreducible-CFG policy.

Original `Label` identity denotes original control-flow provenance. If a future
pass introduces synthetic edge blocks, it must distinguish them from original
blocks rather than treating every new lexical node as an original execution.
Ordinary execution equivalence is not a GPU reconvergence guarantee.

## First Foundation Slice and Migration

This slice unifies the duplicate `RegionJump` and `RegionJump<TValue>` records
on the existing generic constructor. Existing shader-value users migrate:

```text
RegionJump
  -> RegionJump<IShaderValue>

ITerminator<RegionJump, IShaderValue>
  -> ITerminator<RegionJump<IShaderValue>, IShaderValue>
```

All repository callers move together, including analyses, formatters, backends,
lowering passes, and test interpreters. There is no compatibility alias or shim.
Consumers naming the old CLR type or constructing it must update and rebuild;
this is a source/binary API break, not a change to shader execution semantics.
An explicitly typed `ToSuccessor<TE>(...)` call on the old jump terminator becomes
`ToSuccessor<IShaderValue, TE>(...)`, or can use generic type inference.

The small accompanying laws cover payload-map identity/composition,
type-changing maps, label identity, ordered arguments and mapper failures,
lossy control projection, and the real `MapValueUse` caller. Array contents are
compared extensionally: immutable-array storage identity is not semantic
equality of jump arguments.
Identity and composition concern pure mappers; the separate order and exception
cases describe observable behavior for effectful callbacks.

## Subsequent Slices

The next independently scoped steps are to make analysis availability and
continuation absence unambiguous and establish checked region scope,
shared-join ownership, and selected-edge value transfers before target lowering.
The existing Slang target AST deliberately does not claim those general
contracts. Each step must preserve the existing semantic/trace corpus.

See [pass contracts](compiler/passes.md) for per-stage test units and
[functional IR design notes](functional_ir.md) for the original motivation.

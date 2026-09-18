# Linear CIL Analysis and Lossless CFG Construction

## Status and Scope

This is the agreed next frontend design for issue #96, following the shared
constructs in [the IR contract](../ir_spec.md). It supersedes the earlier proposal
to require basic-block construction before stack-type analysis, or to split the
condition/return generic parameters of `ITerminatorSemantic`.

The pipeline below is the target contract. The current implementation now
decodes and retains the complete linear source, computes reachable
pre-instruction stack types, and only then constructs the reachable basic-block
CFG consumed by the runtime-reflection parser. Value lifting remains combined
with parsing; later region and AST stages remain separate work.

### Implemented Frontend Boundary

`MethodBodyAnalysisModel.Instructions` is now the immutable linear view of the
decoded method: one `CilInstructionInfo` per original instruction, with its
index, byte range, and original Lokad instruction object. Each
`CilInstructionBlock` retains its exact non-empty instruction slice and a
`CilControlFlow` value distinguishing native return, branch, and conditional
branch from synthesized fallthrough and end-of-code.

Native controls retain the original final instruction. A conditional retains
its native branch target and physical fallthrough target. Its `ToSuccessor`
projection exposes that native branch/fallthrough order, including two equal
arms. For `brfalse`, the existing lowering visitor still reverses those arms when
it materializes a Boolean `ITerminator.BrIf`; the control projection itself does
not reinterpret the native predicate. The concrete object is not replaced.
`ControlFlowGraphBuilder` derives the graph successor from the created payload's
projection. Its reachable build path partitions only original positions present
in the completed Pre map, so unreachable instructions remain in the linear
source without becoming graph nodes or predecessors.

The `ControlFlowGraphBuilder.Build` source API now requires a three-parameter
node factory `(label, range, successor)` plus the payload's read-only
`ISuccessor` projection. `CilInstructionBlock` changed from a public positional
record struct with public construction, deconstruction, and `with` support to an
internally constructed sealed record obtained from `MethodBodyAnalysisModel`.
Its index/range properties and the model indexer remain computed accessors, not
compatibility constructors. Repository callers migrated together; there is no
compatibility overload or alias.

Branch operands and resolved targets are checked at this boundary. Methods with
exception handling clauses are rejected before CFG construction, and concrete
native controls accept only supported opcode families (`ret`, `br`/`br.s`, and
the supported conditional branches). Unsupported `switch`, exception flow, and
reaching the end of CIL without an explicit return fail explicitly.

`CilPreStackAnalyzer` is a worklist over original instruction indexes. It uses
`CilInstructionInfo.Evaluate` for the existing opcode dispatch, resolves branch
targets without constructing a CFG, and produces
`ImmutableDictionary<int, ImmutableStack<CilStackType>>`. The live parser creates
all basic-block input values from those completed Pre facts and validates its
concrete value stack before every source instruction and at every outgoing edge.
Reachable called-method discovery uses the same Pre key set; dead calls are not
declared or compiled merely because their bytes remain available for diagnostics.

`MethodBodyAnalysisModel(MethodBase)` now creates the raw decoded source model
only. The runtime-reflection frontend completes its internal analysis after
method symbols are available; `PreStackTypes`, `ControlFlowGraph`, labels, and
label mappings reject access before that point. External compilation callers
should obtain completed models through `RuntimeReflectionParser` and its symbol
table rather than treating the model constructor as an eager CFG builder.
Reachable blocks receive their entry stack in the block constructor; no default
empty value represents an uninitialized block.

`RuntimeReflectionParser` distinguishes declarations involved in active
recursive compilation from fully completed method definitions. Recursive calls
may use an in-progress declaration, but a method enters the completed set only
after analysis, reachable-callee compilation, and value lowering all succeed.
Any failure poisons that parser instance, clears its produced method bodies, and
causes later method/body/module compilation attempts to fail explicitly. Its
context is then diagnostic-only; retry requires a new parser with a fresh
compilation context rather than attempting rollback through partially registered
symbols.

The implemented `CilStackType` domain is:

- normalized `Int32` for Boolean and signed/unsigned 8-, 16-, and 32-bit values;
- normalized `Int64` for signed/unsigned 64-bit values;
- distinct `Float32` and `Float64`;
- exact shader value and object-reference types; and
- exact managed-pointer types, including pointee and address space.

Merge requires equal stack height and exact slot equality after the scalar
normalization above. There is no reference least-upper-bound, null widening,
pointer address-space erasure, or pointer-root/alias proof. The existing value
lowering remains responsible for its stricter operation-specific pointer and
storage rules.

Reachable semantics are limited to the operations already implemented by the
runtime-reflection value visitor. Reachable unsupported instructions fail with
method and source context. Syntactic control validation still covers the whole
source, so malformed branch targets and unsupported native controls such as
`switch` are rejected even when dead. Exception flow, indirect loads/stores,
`ldnull`, `dup`, and unsupported unary operations remain unsupported; dead
non-control instructions do not prevent compilation of the reachable method.
Independent CFG-value lifting and later region/AST work remain subsequent
steps.

```text
LinearCode<CilInstruction>
  -> original linear code + completed reachable-position Pre map
  -> reachable CFG<TypedStackBlock>
  -> CFG<ValueBlock>
  -> scoped nested region SSA-like representation
  -> target AST
```

Names in this diagram describe roles, not a requirement to create a new CLR type
for every logical stage. Existing constructor algebras and same-type passes
remain valid.

## Preserve Native Instructions Before CFG Construction

Source adaptation and stack analysis preserve original instruction order,
instruction count, byte offsets, and source information. They do not expand
`beq`, `bne.un`, or other native operations into new instruction sequences.

Resolving a relative branch displacement to an original instruction position
does not rewrite the code. Validate target boundaries explicitly; do not treat
an invalid target as fallthrough. Each original position must remain associated
with its original instruction even when an annotation is attached.

Only after basic-block labels are established should instruction-changing
lowering occur. At that point labels are the semantic control targets; original
byte offsets remain provenance and are not recalculated to maintain references.
The instruction domain is the supported CIL subset, not an implicit promise to
implement every ECMA-335 opcode or exception mechanism.

## Rich Terminators, Narrow Analysis Views

`brtrue`, `brfalse`, `beq`, `bne.un`, and other conditional branches have the same
two-arm control shape. Their different predicates and stack effects belong in
the concrete CIL representation, not in opcode-specific CFG algorithms.

| Native form | Predicate meaning | Stack operands consumed |
|---|---|---|
| `brtrue` | Nonzero/non-null | One |
| `brfalse` | Zero/null | One |
| `beq` | Equal | Two |
| `bne.un` | Unequal, or unordered for applicable floating operands | Two |

Preserve the opcode's signed/unsigned/unordered semantics until the actual
operand types are known. In particular, `.un` does not make every integer
inequality operand an unsigned IR value.

Keep the existing `ITerminatorSemantic` generic arity. An
`ITerminator<Label, Unit>` or successor view can describe control shape without
an explicit condition-value reference. `Unit` is not missing data in that view,
and does not mean every conditional form consumes one stack slot.

Generic topology analysis may ignore predicates. Stack analysis and lowering
must use the concrete instruction/terminator semantics. The concrete object,
including its predicate and source information, remains stored in the typed BB
payload. Observing it through a narrow interface must not replace it with that
projection.

Do not reconstruct and substitute a rich CIL terminator through
`Terminator.Factory<Label, Unit>()`: the projection lacks enough information to
recover the native predicate. Keep the object or use its lossless target mapping,
which preserves all non-target fields. No split of `TE`, hidden recovery table,
generic-analysis downcast, or separate CFG special case for each opcode is
required.

## Pre-Instruction Stack Analysis

The implemented result preserves the full original linear source and associates
Pre states only with reachable original instruction positions:

```text
PreTypes:
    InstructionId -> ImmutableStack<CilStackType>
```

A present entry with an empty stack is a reachable empty-stack position. During
analysis, an absent entry means only that the position has not yet been reached;
do not discard it while propagation is unfinished. Only after successful
completion does absence identify a position that is unreachable from the normal
entry under the supported control-flow model.

This replaces the earlier per-instruction `Unreachable | Reachable` result.
Downstream stages need not carry an unreachable-state variant. Retain original
instructions, positions, and offsets even when their positions are absent from
the completed map.

The algorithm is a type-level abstract interpreter, not execution of the
compiled method. Its inputs include the method signature, arguments, locals,
and resolved call/member metadata. Existing CIL visitor, stack, and type-checking
logic should be inventoried and reused before introducing another implementation.

Separate the worklist driver from three semantic operations:

```text
Transfer(instruction, preStack, methodContext) -> postStack or diagnostic
Successors(instruction)                     -> instruction positions
Merge(existingPre, incomingStack)           -> merged state or diagnostic
```

Start the normal method entry with an empty evaluation stack. Ordinary
instructions propagate to the next position; unconditional branches propagate
only to their target; conditional branches propagate the post-consumption state
to both ordered arms; returns check the method's return requirements and have no
ordinary intraprocedural successor.

Reprocess affected positions when an incoming state changes. This is not a
single physical-order scan: a jump target's predecessor need not be the
instruction immediately before it in the array. The analysis needs control
relations but does not require materializing a basic-block CFG or computing
dominators first.

Consider both conditional arms regardless of observed values or whether a
particular CPU execution took them. This is entry reachability, not constant
propagation or general dead-code optimization. Unsupported reachable operations,
stack conflicts, malformed targets, and incomplete/failed analysis must not be
reclassified as unreachable.

Check stack underflow, height compatibility, slot compatibility, and the
supported opcode's transfer rules. Define `CilStackType` for the actual supported
scalar, value/reference, and managed-pointer cases; do not substitute
`System.Type` or shader types indiscriminately. Unsupported joins and instructions
fail explicitly, never by replacing a stack with an empty/default state.

The exact supported type domain and merge rule are listed in the implemented
frontend boundary above. The type analysis does not establish pointer alias
compatibility or SSA value identity.

All non-scalar stack variants have internal constructors and are produced by the
single `CilStackType.FromShaderType` classifier. The concrete value visitor uses
the same classifier for its evaluation-stack normalization. Built-in reflection
types include `void`, Boolean, signed/unsigned 8/16/32/64-bit integers, and the
supported floating widths; operation-specific value lowering may still reject a
call that the normalized CIL stack verifier accepts.

Exception handlers need additional entry/transfer rules and are outside the
initial design's supported subset. Do not introduce hidden handler behavior or
claim ordinary jump/fallthrough covers exception flow.

Store Pre rather than requiring a separate persistent Post table. The original
instruction and its Pre state determine its supported transfer result. A
completed Post annotation would also be a valid representation, but does not
directly provide the merged entry state needed at a target block.

## Lossless Annotated-Linear to CFG Boundary

Decode the linear source and run Pre analysis before materializing the BB CFG.
The present `MethodBodyAnalysisModel` constructs its CFG eagerly; simply
constructing that old object first and analyzing it afterward does not satisfy
this stage ordering.

Partition at the entry, branch targets, and appropriate control boundaries, and
construct downstream blocks only for reachable positions from the completed Pre
map. A stack BB contains ordinary instructions plus an explicit terminator. Its
entry stack types come from the first instruction's Pre state; splitting does
not re-infer them.

Filter before building the resulting graph's predecessor indexes as well as its
successors. A reachable-only traversal of a graph that still includes dead
predecessors is not a reachable-only CFG. Keep original positions stable rather
than deleting source bytes and renumbering the linear code.

Represent implicit fallthrough explicitly in the CFG without inserting bytes
into the original linear code. A synthesized fallthrough must be distinguishable
from an original CIL control instruction. Retain every reachable original instruction, including the
original terminating instruction, through the BB body/terminator representation.
Unreachable instructions remain available in the original linear source but do
not require fabricated entry types or emitted blocks.

Instruction and callee discovery used for compilation must consistently consume
the reachable view; retaining the full source for diagnostics must not cause a
dead call to be lowered through a separate path. Existing method-level rejection
of unsupported exception handling remains in force.

Use `CFG<TBasicBlock>` to preserve the concrete payload type. CFG and graph
analysis need only a read-only control capability. They must not convert the
payload to `Unit` or rebuild rich blocks from successors alone. A topology-only
`CFG<Unit>` remains valid for a graph that never contained instruction payload.

Successors and predecessor indexes must agree with the BB terminator.
The terminator is the semantic source of control information; any stored
successor view is derived, not independently edited.

Keep ordered conditional arms, including two arms with the same target.
Deduplicated predecessor-node sets are sufficient for some dominance operations,
but are not a substitute for edge/arm identity in merge or value analysis.

## After Labels Are Stable

Native-operation lowering can expand a typed CIL predicate into operations and
an explicit conditional branch. Stack lifting assigns values to stack positions:
entry slots become block parameters and selected outgoing stacks become jump
arguments.

Use one ordering convention: `ImmutableStack.Peek()` is the top; block parameters
and jump-argument arrays are ordered from bottom to top. Type compatibility does
not establish which value arrived or whether pointer aliases can be represented
by the target shader language.

Keep the existing scalar conversion, stable-address pointer, and explicit
unsupported-pattern constraints until separate reviewed changes extend them.
Retain original labels and distinguish synthetic blocks from original blocks.
Do not add region/AST layout or general SSA promotion of mutable locals to these
frontend slices.

## Mapping and Analysis Lifetime

Payload/annotation mapping may change representation while preserving order and
control. A full BB map that changes a terminator can change topology and must not
silently retain topology-dependent analyses. Analysis algorithms and projection
consumers do not need a universal higher-kinded functor interface.

Map identity/composition laws concern pure functions. Lossless remapping preserves
native predicates, original instruction provenance, and non-target fields.
Projecting to control shape and reconstructing from that projection is not a
lossless round trip.

## Small Implementation Slices and Acceptance

1. **Implemented — lossless source/control boundary:** explicit linear representation or view,
   concrete native CIL control retained through CFG construction, generic
   projections, and at least one real existing frontend consumer.
2. **Implemented — integrated Pre-stack analysis:** reuse supported CIL semantics, compute
   completed entry states on instruction positions, check joins before BB
   formation, and wire the result into reachable CFG construction and the live
   frontend. An unused analysis helper is not completion.
3. **Planned — independent CFG-value lifting:** consume those annotations, assign stable labels
   and ordered block values, and separate the existing parser's fused steps.

Each slice must have its own narrow contract and current-versus-planned status.
Avoid creating an unused IR scaffold or a parallel general interpreter.

Minimum cases include native predicate distinctions, forward/backward targets,
conditional and ordinary fallthrough, a separately labeled final instruction,
same-target parallel arms, annotation identity/order, stack joins and mismatches,
unreachable versus empty stacks, and loop-carried values. Test preservation of
concrete predicate/source data as well as the generic control projection.

Preserve all existing Debug and actual Release compiler/oracle expectations.
Before claiming a stage is integrated, run its real consumer and retain explicit
failures for inputs outside the supported subset.

# Linear CIL Analysis and Lossless CFG Construction

## Status and Scope

**Implemented collection contract:** [parse a complete all-reference CIL module](passes.md#implemented-boundary-parse-a-complete-cil-module).
The parser recursively collects all original-CIL method/type/field references,
including unreachable instruction positions, up to explicit builtin/intrinsic
boundaries and ends at raw CIL IR. Pre and later transformations are explicit
typed passes. This changes module membership, not the later per-function
reachable-instruction analysis.

This is the implemented frontend design for issue #96, following the shared
constructs in [the IR contract](../ir_spec.md). It supersedes the earlier proposal
to require basic-block construction before stack-type analysis, or to split the
condition/return generic parameters of `ITerminatorSemantic`.

The current implementation decodes and retains the complete linear source,
computes reachable pre-instruction stack types, partitions a labelled CIL block
list, lowers it to typed shader-stack blocks, constructs the shader-stack CFG,
and mechanically lifts it to a flat value CFG. A dedicated pass publishes
existing control-flow results as BB-local annotations; region organization
consumes those annotations without reanalysis. Target AST separation remains
later work.

### Issue #114: Implemented frontend path

```text
Raw CIL LinearCode -> strict Pre -> Typed CIL LinearCode
  -> CilBlockPartitionPass -> LabelledCilFunctionBody
  -> CilToShaderStackPass -> ShaderStackFunctionBody
  -> ShaderStackControlFlowPass -> ShaderStackControlFlowBody
  -> ShaderStackToValuePass -> CilValueControlFlowBody
  -> CilLocalPromotionPass -> CilValueControlFlowBody
  -> existing control facts / regions
```

The public CIL CFG and `MethodBodyAnalysisModel` boundary no longer exist.
`ShaderStackOperand` is either a typed depth into one pre-operation snapshot or
a resolved literal/function/stable-address symbol. `ShaderStackInstruction`
contains operations with explicit pop counts, stable alias pushes, or drops.
Every emitted instruction and terminator has a derived typed transition and
numeric original-index/byte-range/ordinal provenance. Instruction normalization
is separate from mechanical stack elimination. The following public stage
promotes only definitely assigned direct nonescaping `i32` and `bool` locals;
escaped, unsupported and incompletely initialized locals remain in storage.
Region, target-AST and wider call support remain separate.

### Implemented Frontend Boundary

`CilMethodDecoder.Decode` now returns the immutable
`LinearCode<CilInstructionInfo>` view: one `CilInstructionInfo` per original instruction, with its
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
`InstructionBlockPartitioner` partitions only original positions present in the
completed Pre map, so unreachable instructions remain in the linear source
without becoming block definitions or predecessors. A gap starts a new block,
but ordinary fallthrough cannot skip a gap; every explicit arm from a reachable
instruction must target a reachable original position.

`BlockList<TBlock>` is a thin immutable ordered `ImmutableArray<TBlock>` plus an
explicit `EntryLabel`. Payloads implement the existing `ILabeledEntity` and own
their labels and concrete control. Construction validates initialized, nonempty
storage, unique label identities, entry membership, and membership of every
projected control target. Equal label names do not imply equal labels. Generic
lists may contain disconnected definitions and need not store the entry first;
the CIL model separately enforces complete reachable-source coverage.

`InstructionBlockPartitioner.Build` / `BuildReachable` accept a block factory
`(label, range, successor)`, a pure payload-to-`ISuccessor` projection, and a fixed
`BlockList<TBlock>` printer. All labels are bound before payload construction;
factories must retain those labels. Original ranges are not compacted. The
partitioner remains mutable pass-local scratch; published lists do not change.
`ControlFlowGraph.Create(blocks, projection, optionalGraphPrinter)` then indexes
**all stored definitions**, not the reachable RPO returned by `CFG.Labels()`.
It retains the exact payload objects and ordered successor arms, including
duplicates. No predecessor, RPO, dominance, completion cache, or parallel
successor collection is stored in `BlockList`; projections derive from immutable
payloads and are not retained there.

`CilInstructionBlock` changed from a public positional
record struct with public construction, deconstruction, and `with` support to an
internally constructed sealed record obtained from `CilBlockPartitioner`.
Its index/range properties and the model indexer remain computed accessors, not
compatibility constructors. Repository callers migrated together; there is no
compatibility overload or alias.

Branch operands and resolved targets are checked by the Pre/CFG boundary.
Raw parsing preserves methods with exception handling clauses; `CilPreStackPass`
rejects them before propagation and CFG construction. Concrete
native controls accept only supported opcode families (`ret`, `br`/`br.s`, and
the supported conditional branches). Unsupported `switch`, exception flow, and
reaching the end of CIL without an explicit return fail explicitly.

`CilPreStackAnalyzer` is a worklist over original instruction indexes. It uses
`CilInstructionInfo.Evaluate` for the existing opcode dispatch, resolves branch
targets without constructing a CFG, and produces
`LinearCode<Annotated<CilInstructionInfo, PreStack>>`.
`CilToShaderStackPass` validates its typed stack before every source instruction
and at every outgoing edge. `ShaderStackToValuePass` consumes only shader-stack
operations and transitions; it does not inspect opcodes, reflection metadata, or
`CilStackType`. Method discovery is independent: the raw collector
walks every original instruction operand, so dead calls and their recursive
reference closure are included in module membership.

`CilMethodEnvironment` owns the immutable signature, locals, source offsets, and
offset lookup shared by both linear values. `CilPreStackAnalyzer` returns
`LinearCode<Annotated<CilInstructionInfo, PreStack>>` only after successful
analysis. `CilBlockPartitioner` consumes that completed value and the raw source.
`LabelledCilFunctionBody` retains the exact annotated instruction slices and
checks that execution begins at original instruction zero, all definitions are
entry-reachable, and the blocks exactly partition completed Pre while preserving
instruction and annotation identity.

`RuntimeReflectionParser` declares a method before scanning its body, so repeated
and mutually recursive references terminate. It freezes the complete shared
symbol snapshot only after closure collection finishes, then publishes
`RawCilFunctionBody` values with immutable per-method local/argument views.
All type-bearing roots use the same recursive collector, including locals,
module-variable types, static declaring types, base types and inherited layout
fields. Function-pointer signatures recursively contribute return and parameter
types before stopping at a non-layout boundary; runtime `calli` support is not
implied. Type visitation is recorded before member traversal to terminate
reference cycles. Unsupported recursive value layouts fail contextually.
Collection does not execute methods, intrinsic stubs, constructors, or static
initializers. A decode or metadata-resolution failure publishes no module.
Failures in later passes do not invalidate or mutate an already returned raw
module.

Shader module variables are field-backed. An attributed property is rejected
during metadata collection because the current CIL frontend has no property
getter/backing-field variable ABI. The parser does not publish a property
declaration that later symbol lookup cannot consume.

Frozen symbol views snapshot mutable `CompilationContext` parents recursively.
Later parent mutation cannot change lookup results in a previously returned raw
module. Every public collection operation, including direct type/field/parameter
parsing and module variable/entry discovery, uses the same fail-closed lifecycle.
Failure poisons that parser before a cached placeholder or partial module can be
returned; recursive helpers remain inside the active operation.

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
method and source context in the responsible later pass. Syntactic control
validation still covers the whole source, so malformed branch targets and
unsupported native controls such as `switch` are rejected even when dead.
Exception flow, `initobj`, indirect
loads/stores, `ldnull`, `dup`, and unsupported unary operations remain
unsupported. `initobj` is rejected at shared instruction dispatch because the
value frontend does not yet emit its required zero-initialization store;
ordinary `pop` remains supported. Dead non-control instructions in one collected
function remain absent from that function's Pre/CFG. A separately collected dead
callee is nevertheless compiled by the module pipeline and may fail on its own
reachable unsupported semantics; this is the intentional all-reference policy.

```text
LinearCode<CilInstruction>
  -> original linear code + completed reachable-position Pre map
  -> labelled BlockList<TypedStackBlock>
  -> reachable CFG<TypedStackBlock>
  -> CFG<ValueBlock>
  -> scoped nested region SSA-like representation
  -> target AST
```

Names in this diagram describe roles, not a requirement to create a new CLR type
for every logical stage. Existing constructor algebras and same-type passes
remain valid.

## Read-Only Stage Diagnostics

Each stage implements the existing `IPrintable` contract with a fixed
representation:

```csharp
LinearCode<CilInstructionInfo> raw = CilMethodDecoder.Decode(method);
Console.Write(raw.PrettyPrint());

var rawModule = new RuntimeReflectionParser().ParseMethod(method);
var preModule = CilPreStackPass.Run(rawModule);
var labelledModule = CilBlockPartitionPass.Run(preModule);
var stackModule = CilToShaderStackPass.Run(labelledModule);
var stackCfgModule = ShaderStackControlFlowPass.Run(stackModule);
var valueModule = ShaderStackToValuePass.Run(stackCfgModule);
var promotedValueModule = CilLocalPromotionPass.Run(valueModule);
Console.Write(preModule.FunctionDefinitions.Values.Single().Code.PrettyPrint());
Console.Write(labelledModule.FunctionDefinitions.Values.Single().Blocks.PrettyPrint());
Console.Write(stackModule.FunctionDefinitions.Values.Single().Blocks.PrettyPrint());
Console.Write(stackCfgModule.FunctionDefinitions.Values.Single().Graph.PrettyPrint());
Console.Write(valueModule.FunctionDefinitions.Values.Single().Dump());
Console.Write(promotedValueModule.FunctionDefinitions.Values.Single().Dump());
```

The interface operation also accepts an `IndentedTextWriter` and
`PrettyPrintOption`. Raw printing reads only decoded instructions. Completed
linear printing intentionally contains only reachable annotated positions; use
the separate raw value to print dead source. Printing never starts analysis or
lowering.

For example, these are the actual Debug outputs for
`int Choose(bool choose, int left, int right) => choose ? left : right`, asserted
by `CompilerStageDumpTests.ChooseDumpsActualConfigurationSpecificLinearCilAndCfg`.
Before partitioning:

```text
linear-cil pre-annotated reachable (byte ranges are half-open; stack order: bottom -> top)
#0 IL_0000..IL_0001 ldarg.0 pre=[]
#1 IL_0001..IL_0003 brtrue.s rel=+3 resolved=IL_0006 pre=[i32]
#2 IL_0003..IL_0004 ldarg.2 pre=[]
#3 IL_0004..IL_0006 br.s rel=+1 resolved=IL_0007 pre=[i32]
#4 IL_0006..IL_0007 ldarg.1 pre=[]
#5 IL_0007..IL_0008 ret pre=[i32]
```

After partitioning, `CilBlockPartitioner.Partition(raw, pre).PrettyPrint()`
(an internal compiler/test boundary) produces:

```text
labelled-cil-block-list (storage order; byte ranges are half-open; stack order: bottom -> top)
entry=^0(0x0)
^0(0x0) instructions=#0..#1 bytes=IL_0000..IL_0003 entry=[]
    control: native brtrue.s rel=+3 resolved=IL_0006 taken=^2(0x6) fallthrough=^1(0x3)
^1(0x3) instructions=#2..#3 bytes=IL_0003..IL_0006 entry=[]
    control: native br.s rel=+1 resolved=IL_0007 target=^3(0x7)
^2(0x6) instructions=#4..#4 bytes=IL_0006..IL_0007 entry=[]
    control: synthetic fallthrough target=^3(0x7)
^3(0x7) instructions=#5..#5 bytes=IL_0007..IL_0008 entry=[i32]
    control: native ret
```

List printing uses storage order and assigns diagnostic IDs in that order. It
does not construct a CFG or run graph algorithms. The same test checks the
different optimized Release shape (two returning arms, without the separate
fallthrough/return blocks). `BlockListPrintsStorageOrderIncludingDisconnectedDefinitionsAndNonFirstEntry`
also exercises reordered storage, a non-first entry, and disconnected payloads.

Byte ranges are half-open and stack entries are printed bottom to top. `rel` is
the encoded displacement; `resolved` is its original IL target. The completed
linear view omits unreachable positions without renumbering later instructions.
The raw view remains the authoritative complete-source diagnostic. Shader-stack
diagnostics show each expansion's `#original.ordinal`, numeric byte span,
typed `pre`/`post`, explicit depth operands and pop count. Captured outputs are
maintained by the compiler-stage tests rather than duplicated here.

### Breaking API migration

Parsing and compilation are now separate public operations without compatibility
shims:

| Removed API | Replacement |
|---|---|
| `ControlFlowGraphBuilder` and its combined CFG-building `Build` / `BuildReachable` | `InstructionBlockPartitioner.Build` / `BuildReachable` -> `BlockList<TBlock>`, then `ControlFlowGraph.Create(blocks, projection, graphPrinter)` |
| A partitioner factory returning an unlabelled payload | A payload implementing existing `ILabeledEntity`, retaining the supplied label |
| The old partitioner CFG printer callback | A fixed `Action<BlockList<TBlock>, IndentedTextWriter, PrettyPrintOption>`; CFG printing is selected separately at graph construction |
| `new MethodBodyAnalysisModel(method)` | `CilMethodDecoder.Decode(method)` |
| `model.Instructions` / `model.PreStackTypes` | `model.RawCode` / `model.PreAnnotatedCode` |
| `RuntimeReflectionParser.ParseMethod(...) -> FunctionDeclaration` | `ParseMethod(...) -> ShaderModuleDeclaration<RawCilFunctionBody>` |
| `RuntimeReflectionParser.ParseShaderModule(...) -> ShaderModuleDeclaration<FunctionBody4>` | `ParseShaderModule(...) -> ShaderModuleDeclaration<RawCilFunctionBody>` |
| `CLSLCompiler.Parse(...) -> ShaderModuleDeclaration<FunctionBody4>` | `Parse(...)` for raw CIL; `Compile(...)` for `FunctionBody4` |
| `parser.MethodBodies` / `ParseMethodBody3` | `CilPreStackPass` -> `CilBlockPartitionPass` -> `CilToShaderStackPass` -> `ShaderStackControlFlowPass` -> `ShaderStackToValuePass` -> `CilLocalPromotionPass` -> `CilBlockControlFactsPass` -> `CilRegionPass` |
| Public CIL CFG / `MethodBodyAnalysisModel` | `LabelledCilFunctionBody.Blocks`; the first CFG is `ShaderStackControlFlowBody.Graph` |
| `MethodBodyAnalysisModel.CilInstructionBlock` | `CilInstructionBlock` in `DualDrill.CLSL.Frontend` |
| `block.Instructions[i]` as a bare CIL instruction | `block.Instructions[i].Node`, with `.Annotation` holding its `PreStack` |
| `DumpRawLinearCil()` | `RawCode.PrettyPrint()` |
| `DumpAnalyzedLinearCil()` | `PreAnnotatedCode.PrettyPrint()` |
| `DumpReachableControlFlowGraph()` | `ShaderStackControlFlowBody.Graph.PrettyPrint()` |

`ISymbolTable` no longer stores compiled body caches. Clients that predeclare a
reflection method add its `FunctionDeclaration`; parsing freezes a symbol view
into the raw module, and later passes consume only that returned module.

Every `Annotated<TNode, TAnnotation>` carries a readonly typed printer chosen by
its producer and implements `IPrintable` directly. Equality and hashing compare
only `Node` and `Annotation`; presentation is not analysis identity. Instruction
annotations print the instruction and its entry stack together. The labelled CIL list, labelled shader-stack list, shader-stack CFG, flat value
CFG and BB-annotated value CFG have fixed readable formats.
After `CilLocalPromotionPass`, `CilBlockControlFactsPass` attaches each block's
RPO, IDom, ordered incoming arms and typed finite-exit `PostDominance`
(`Block`, `FunctionExit` or `NoExitPath`), including structural `MayDiverge`.
Loop-header status is derived from incoming dominance-backed arms.
`CilRegionPass` reads those annotations; it neither queries a separate analysis
object nor reruns it. Postdominance does not establish continuation ownership.
Type-changing annotation maps must supply a printer for the output types; the
identity and composition laws concern mapped `Node` and `Annotation` data, not
reuse of an incompatible presentation function.

Use the existing `FunctionBody4.Dump` and shader-module formatter for the
subsequent region and module stages. Nested region bindings in that dump describe
structural ownership, not a linear execution sequence. A loop's
`break -> <not recorded>` means that continuation metadata was not populated at
that stage; it does not assert that the loop has no exit or that an exit is
unreachable.

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
supported floating widths. These registrations provide the metadata required by
the documented stack domain; they do not broaden ordinary call acceptance, and
operation-specific value lowering may still reject a call that the normalized
CIL stack verifier accepts.

Exception handlers need additional entry/transfer rules and are outside the
initial design's supported subset. Do not introduce hidden handler behavior or
claim ordinary jump/fallthrough covers exception flow.

Store Pre rather than requiring a separate persistent Post table. The original
instruction and its Pre state determine its supported transfer result. A
completed Post annotation would also be a valid representation, but does not
directly provide the merged entry state needed at a target block.

## Lossless Annotated-Linear to Block-List to CFG Boundary

Decode the linear source and run Pre analysis before materializing the BB CFG.
The concrete signatures enforce this order:

```text
CilMethodDecoder.Decode
  -> LinearCode<CilInstructionInfo>
CilPreStackAnalyzer.Analyze
  -> LinearCode<Annotated<CilInstructionInfo, PreStack>>
CilBlockPartitioner.Partition
  -> BlockList<CilInstructionBlock>
CilToShaderStackPass
  -> BlockList<ShaderStackBasicBlock>
ShaderStackControlFlowPass / ControlFlowGraph.Create
  -> ControlFlowGraph<ShaderStackBasicBlock>
ShaderStackToValuePass
  -> ControlFlowGraph<CilValueBasicBlock>
CilLocalPromotionPass
  -> ControlFlowGraph<CilValueBasicBlock>
```

`CilBlockPartitionPass` retains the raw/Pre source-association and
source-coverage checks. Generic graph construction occurs only after every CIL
instruction has been lowered to explicit shader-stack operations.

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

Instruction lowering consumes the reachable per-function view. Module collection
instead consumes all original instruction operands, so dead calls contribute
declarations and recursively collected bodies without fabricating reachable CFG
nodes. Existing method-level rejection of unsupported exception handling occurs
in `CilPreStackPass`.

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
The following bounded promotion stage handles only direct nonescaping `i32` and
`bool` locals. Do not add region/AST layout or broader mutable-local SSA
eligibility to these frontend slices.

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

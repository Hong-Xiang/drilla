# Shared IR Constructs and Stage Contracts

This is the canonical contract for organizing CLSL's intermediate
representations. It distinguishes implemented representations from intended
stage invariants. An invariant listed here is not a claim that a corresponding
validator or transformation already exists.

The design uses a small set of generic constructors, not a separate class
hierarchy for every pass. An optimization or lowering may legitimately return
the same CLR type it consumes. Nested region IR and target-language AST remain
different logical stages, even when they reuse constructors.

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
  -> method declarations and original MethodBodyAnalysisModel.Instructions
  -> CilPreStackAnalyzer with method symbols
  -> sparse PreStackTypes and reachable ControlFlowGraph<CilInstructionBlock>
  -> RuntimeReflectionParser.ParseMethodBody3
  -> FunctionBody4
  -> FunctionToOperationPass                  : FunctionBody4 -> FunctionBody4
  -> RegionParameterToLocalVariablePass       : FunctionBody4 -> FunctionBody4
  -> SlangEmitter
  -> Slang source
  -> slangc                                  : Slang source -> WGSL
```

`FunctionBody4` currently combines typed instructions and parameterized CFG
terminators with a `RegionTree` built from dominance containment. The frontend
first computes Pre stack types on the original linear code and constructs only
the reachable CFG. The parser then uses those facts for block inputs while
performing stack-to-value translation and region-tree construction. The emitter
still performs lexical layout. There is not yet an independent scoped-region
validator, complete structurization pass, or target AST stage.

`ExprValue`/`ExprTree` and the `AbstractSyntaxTree` directory do not constitute a
complete AST function-body stage in this pipeline. Older design examples,
experimental backends, and the identity `CommonOperationLoweringPass` must not
be presented as additional active compilation stages.

## Logical Stages and Their Obligations

These boundaries split reasoning and testing; they do not require six unrelated
IR implementations. The last two rows describe intended stages, not completed
implementations.

| Stage | Required invariant | Current owner or implementation boundary |
|---|---|---|
| Linear CIL | Instruction boundaries and branch offsets are resolved consistently. | `MethodBodyAnalysisModel`; unsupported instructions remain explicit failures. |
| Linear Pre facts | Reachable entries have exact normalized stack types; absent entries are unreachable only after successful completion. | `CilPreStackAnalyzer`, before CFG construction; full original source is retained. |
| CFG of CIL blocks | Reachable instruction ranges are partitioned correctly; explicit terminators and legitimate fallthrough edges are preserved, without dead predecessors. | `ControlFlowGraphBuilder.BuildReachable` and the completed Pre facts. |
| Typed CFG with block arguments | Each block has one terminator; edge arity/types agree with destination parameters; values are available on the selected path. | `RuntimeReflectionParser` and `ShaderRegionBody` build this representation. Validation is partial, not a complete verifier. |
| Operation/value lowering | The transformation preserves control identities and effects while establishing its declared operation or parameter postcondition. | Existing same-type passes; their current restrictions are described below. |
| Scoped nested region SSA-like IR | Every shared join has a defined owner; loop/continuation transfers resolve within permitted scopes; edge arguments and definition sharing remain explicit. | Intended contract. Dominance containment alone does not establish it. |
| Target AST | Control targets have a legal target-language realization; shared joins and value transfers have explicit lexical placement; effects retain their order and dynamic multiplicity. | Intended lowering. Current `SlangEmitter` combines these decisions with text emission. |

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

During Slang emission, the current emitter supports a lexical loop with zero or
one distinct non-terminating destination outside its existing region subtree. Direct
terminal targets retain their actions and return in the selected arm. Multiple
distinct normal destinations are explicitly rejected. This emission-time layout
does not transform the input IR into a checked scoped-region representation.
It is not the complete Beyond Relooper algorithm or a general irreducible-CFG
policy.

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
continuation absence unambiguous, establish checked region scope and shared-join
ownership, and lower those owned references and values into a target AST.
Expression tree packing and final source formatting need not be the same pass
as control layout. Each step must preserve the existing semantic/trace corpus.

See [pass contracts](compiler/passes.md) for per-stage test units and
[functional IR design notes](functional_ir.md) for the original motivation.

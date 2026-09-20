# Proposed GPU Reconvergence Contract

Status: **specification oracle only**. This document and
`ReconvergenceContractTests` do not change compiler output, accept new shaders,
or prove GPU behavior. They refine the direction in
[the research snapshot](./maximal-reconvergence-research.md).

Compiler baseline: `fb7304a30b9102ac770ba7547512dd9bdf65c9d3` (post-#121).
This is a proposed contract for human approval, not an approved public runtime
semantics change. Original C# nesting erased before CIL is not reconstructed.

## Chosen initial contract

The semantic input is the reachable, typed `CilValueControlFlowBody` after local
promotion, with original labels and ordered control arms, followed by checked
`RegionFunctionBody.Control` (`Forward`/`Repeat` and lexical owner).

A future cooperative profile may admit only explicitly checked, single-entry
reducible selections and loops with declared compatible merge, continue and
exit relationships. Escapes must be classified against those enclosing
relationships. Ambiguous, irreducible or otherwise unproved shapes are
unsupported for precise cooperation; they must not be guessed from immediate
postdominators.

The current `RegionFunctionBody` does **not** declare these relationships.
`RegionTree.Create` currently supplies null `Next`/`BreakNext` values, while
postdominance and scoped `Forward`/`Repeat` facts establish scalar continuation
and lexical visibility only. None is a GPU merge certificate.

Dynamic instruction identity includes:

- the declared relationship and its dynamic activation;
- its parent and call path;
- loop entry and current iteration;
- branch or switch partition where applicable; and
- repeated execution of one observation site.

A static block label alone is insufficient. Observations are a multiset of
site, dynamic context and sorted participant set. An empty set does not execute.

### Relationship rules

- A selection merge reunites the non-escaping members of that selection entry.
- A continue target reunites continuing members of that loop iteration.
- A loop exit reunites surviving members of that loop entry, even when they
  exited on different iterations.
- Returning from a callee ends its internal relationships; participants of that
  call instance rejoin at its related caller continuation.
- Returning from the entry point has no caller continuation.
- An escape removes the frames it leaves, but preserves enclosing contexts.
- Every required reconvergence assumes finite progress to the related location.

These rules say nothing by themselves about value uniformity, legal derivative
quads, workgroup-barrier uniformity, memory visibility or forward progress.
Those are separate checks.

## Fixed switch policy

Table arm ordinal, default identity, target and tangle identity are distinct.
For one incoming switch tangle:

- equal selector values remain together;
- different selector values selecting one target may remain separate or unite;
- values selecting distinct targets do not share a case-entry tangle; and
- the selected partition policy is fixed for one compilation and reused by
  later dynamic activations.

For selectors `[0,0,1,9]` and targets `A,A,A,D`, `A` is exactly either
`{{0,1},{2}}` or `{{0,1,2}}`. For selectors `[0,0,2,9]` all targeting `A`,
the three selector groups have exactly the five set partitions. An ordinary CFG
branch to `A` is not part of an unrelated switch activation.

## Executable oracle

`DualDrill.CLSL.Test/ReconvergenceContractTests.cs` supplies immutable lane walks
made only from structural `Enter`, `Next`, `Leave` and `Observe` events plus
branch/switch choices. `Visit` records original CFG labels for the independent
scalar trace projection. Inputs do not contain expected observation contexts or
participant sets; candidate switch partitions are checked before becoming a
compiled partition policy.

The small reducer maintains a nested context stack and activation counters. It
derives call instances, loop entries/iterations, merge/return contexts and
switch tangles, then groups matching observations. `Next` and outward `Leave`
remove only the escaped inner frames. This is deliberately a reducer for the
bounded fixtures, not a CFG interpreter, scheduler, Region validator or GPU
simulator.

Literal expectations cover:

- full and partial diamonds;
- staggered loop exits;
- explicit continue plus ordinary-tail union;
- nested continue/break escapes;
- entry return and early callee return;
- sequential, distinct-caller and per-loop-iteration call instances;
- both shared-target switch partitions and all five default-alias partitions;
- incomplete, duplicate, split-equal-selector and cross-target negatives;
- an ordinary branch into a case target;
- repeated observations as distinct multiset entries; and
- exact outputs from deliberately early and late mutant reducers.

The scalar counterexample uses identical CFG label and operation/result traces,
with different declared loop-merge relationships on the same scalar CFG:

```text
shared CFG: entry -> head; head -> head or P; P -> X
lane n: take n backedges, then visit P (record 7), then X
proposed A: L's merge is X; P is on the iteration's break path
proposed B: L's merge is P; P executes after leaving L

lane0 entry -> head -> P -> P=7 -> X
lane1 entry -> head -> head -> P -> P=7 -> X
lane2 entry -> head -> head -> head -> P -> P=7 -> X
lane3 entry -> head -> head -> head -> head -> P -> P=7 -> X
```

Both walks must match these literal scalar traces. The differing relationship
annotations are deliberately outside that scalar projection. This illustrates
why a chosen programming model is necessary, not how to recover erased nesting.

Captured CPU-model output:

```text
ACTUAL MODEL scalar/before-exit
P/L#0/i0={0}
P/L#0/i1={1}
P/L#0/i2={2}
P/L#0/i3={3}
ACTUAL MODEL scalar/after-exit
P/L#0/exit={0,1,2,3}
```

The two programs therefore have the same scalar projection but different
cooperative participation.

Values are deliberately excluded from participation keys. Another vector has
`P` values `3,5,7,11` for lanes `0,1,2,3` and still yields
`P/root={0,1,2,3}`. Convergence does not require value uniformity.

### Input/output catalogue

All CFG/relationship notation in this table is **proposed**, not an existing
formatter's syntax. Output is the literal expectation checked against the CPU
model. Every row is **model-only**: no generated Slang/WGSL/SPIR-V and no GPU
execution for these vector programs. Standalone target capability probes below
are separate evidence, not compiled versions of these programs.

| Input CFG and declared relationships | Exact observations (sets abbreviated where context is stated) |
|---|---|
| `E -> even A / odd B -> M`; selection `S`, entry `{0,1,2,3}` | `A/S#0/arm-even={0,2}`, `B/S#0/arm-odd={1,3}`, `M/S#0/merge={0,1,2,3}` |
| Same diamond, entry `{0,1,2}` | `A={0,2}`, `B={1}`, `M={0,1,2}` in the same `S#0` contexts |
| `L: lane==i -> break -> X; otherwise C -> L`; loop `L`, iteration `i` | `break/L#0/i0={0}`, `i1={1}`, `i2={2}`, `i3={3}`; `C/L#0/i0={1,2,3}`, `i1={2,3}`, `i2={3}`; `X/L#0/exit={0,1,2,3}` |
| `L/i0: lane0 break; lane1 continue; lanes2,3 tail -> continue`; remaining lanes break at `i1` | `tail/L#0/i0={2,3}`; `C/L#0/i0={1,2,3}`; `break/L#0/i0={0}`, `i1={1,2,3}`; `X/L#0/exit={0,1,2,3}` |
| Selection nested in `L`: lane0 breaks out; lane1 continues, then exits | `C/L#0/i0={1}`, `X/L#0/exit={0,1}`; inner selection does not survive either escape |
| Selection `S`: lane0 returns from entry; lanes1,2,3 reach `M` | `M/S#0/merge={1,2,3}` |
| Call `K`, inner selection `T`: even lanes return early; odd lanes execute `H` then return | `H/K#0>T#0/arm-body={1,3}`; `after-call/K#0/return={0,1,2,3}` |
| Execute `K` twice; separately call same helper from `K` then `J`; separately call `K` in loop iterations 0 and 1 | Each call has `H={1,3}`, after-call `{0,1,2,3}`. Distinct contexts are `K#0`/`K#1`, `K#0`/`J#0`, and `L#0/i0>K#0`/`L#0/i1>K#0`; helper suffix is always `>T#0/arm-body` |
| Partial caller selection `S`, lanes `{0,1,2}`, even/odd arms both call `K` | `H/S#0/arm-odd>K#0>T#0/arm-body={1}`; after-call even `{0,2}`, odd `{1}` in distinct caller contexts; `after-caller/S#0/merge={0,1,2}` |
| Switch `W`: selectors `[0,0,1,9]`; case0/1 -> `A`, default -> `D`, merge `M` | `A={{0,1},{2}}` or `{{0,1,2}}`; `D={3}`; `M/W#0/merge={0,1,2,3}`. Repeat as `W#1` with the same compiled partition policy |
| Switch `W`: selectors `[0,0,2,9]`; case0 and default -> `A` | Exactly `{{0,1,2,3}}`, `{{0,1,2},{3}}`, `{{0,1,3},{2}}`, `{{0,1},{2,3}}`, or `{{0,1},{2},{3}}`; merge all four |
| Switch entered only by lanes0,1,3; lane2 reaches `A` by ordinary branch | `A/W#0/t0={0,1}`, `A/root={2}`, `D/W#0/t1={3}`, `M/W#0/merge={0,1,3}` |
| `Q` observed twice without changing scope, lanes0,1 | `Q/root={0,1}`, `Q~1/root={0,1}`; two multiset entries, not one |
| Same CFG `entry -> head; head -> head or P; P -> X`, but declare loop merge at X versus P | Four singleton `P/L#0/iN` versus one `P/L#0/exit`, as captured above; identical per-lane original-label and `P=7` traces |
| Wrong early mutant ignores context; wrong late mutant retains diamond arm at merge | Early: `P={0,1,2,3}` instead of four iterations. Late: `M/S#0/arm-even/merge={0,2}`, `M/S#0/arm-odd/merge={1,3}` instead of one merge; both rejected |

The ordinary-branch vector isolates relationship identity; it is not asserted
to be a valid final SPIR-V CFG. A future backend must additionally check the
extension's structural requirements.

Run the executable vectors without a GPU:

```sh
nix develop --builders '' --command dotnet test \
  DualDrill.CLSL.Test/DualDrill.CLSL.Test.csproj --no-restore \
  --configuration Debug --filter FullyQualifiedName~ReconvergenceContractTests \
  --logger 'console;verbosity=detailed'
```

Use `--configuration Release` for the other configuration; a fresh worktree may
need the existing project restored after a missing-assets error.

### Proposed shape versus captured output

```text
proposed diamond relationship:
E --even--> A --\
 \--odd ---> B ---> M(selection S)

captured CPU model:
A/S#0/arm-even={0,2}
B/S#0/arm-odd={1,3}
M/S#0/merge={0,1,2,3}

generated Slang/WGSL/SPIR-V for this vector: absent
GPU execution for this vector: absent
evidence class: model-only
```

The test output is an actual CPU execution of the specification reducer. The CFG
and relationship snippets are proposed inputs. No target text shown here is
hypothetical output.

## Current pipeline and preservation threats

The post-#121 public path is:

```text
Raw CIL -> Pre -> labelled CIL -> ShaderStack -> CFG -> explicit values
-> local promotion -> block control facts -> checked Region
-> FunctionToOperationPass -> stable pointer lowering
-> Slang target AST -> Slang emitter -> slangc -> WGSL
```

`FunctionToOperationPass` normalizes recognized operation and constructor calls;
it does not inline ordinary calls. Cooperative requirements must therefore be
classified after that pass and propagated transitively through remaining calls.
Unknown effects cannot default to pure.

Current target lowering can alter cooperative structure while preserving scalar
behavior:

- `SlangTargetLowering.LowerRaw` wraps original labels in `SlangDoOnce`;
- `LowerActivation` adds carrier nesting and control-token gates; and
- `LowerRegion` realizes repeats and outward transfers with loop
  continue/break constructs.

One placement per original label and correct selected-edge parallel copies are
not enough to prove participation preservation. A future dedicated analysis
must create and freeze relationship identities before target lowering.
Intervening same-body-type passes must explicitly preserve them rather than
recompute them from transformed control flow.

| Existing boundary | Future preservation obligation |
|---|---|
| CIL decoding, Pre, partition, stack/value lifting, local promotion | Preserve supported instruction ordering, original label identity, canonical types and ordered selected-edge tuples; document any control normalization before selecting the contract |
| Control facts -> checked Region | Select explicit loop-entry/iteration/continue/exit and selection relationships in a dedicated analysis; scalar postdominance alone is insufficient, especially for nontermination/escapes |
| Operation-call normalization and ordinary calls | Classify exposed operations and compute transitive helper requirements before deciding target eligibility; preserve origin/relationship identity when rewriting a call |
| Stable pointer/value parameter lowering | Preserve control, selected-edge parallel binding and operation placement; newly introduced accesses do not confer memory synchronization |
| Slang target lowering and downstream Slang optimization | Establish a correspondence for synthetic carriers, gates, duplicated/moved operations and final merges; otherwise reject precise-cooperation mode |
| Text emitter | Print an already justified target AST; never choose graph regions or merge points here |

Label renaming and storage-order changes must not change relationships.
Branch arm order and original label identity are meaningful inputs. A later
node split, loop unroll, inlining, dispatcher or common-tail rewrite needs a
relationship-preservation argument, not just matching per-invocation traces.

## Proposed operation policy

These restrictions are a future admission policy, **not checks implemented by
this slice**. Effects are separate requirements, not one `convergent` flag:

| Operation | Proposed admission obligation |
|---|---|
| Subgroup ballot/count/shuffle/reduction | Require the relevant target feature and operand-specific validity. Baseline compute WGSL requires workgroup-uniform control for subgroup builtins; subgroup-scoped uniformity is available only when `subgroup_uniformity` is supported and enabled. Do not promise exact partial-tangle results from divergent WGSL |
| Derivatives and implicit-derivative sampling | Require the permitted shader stage, derivative-uniform control and valid quad participation; diagnostic suppression is not a guarantee |
| Workgroup control barrier | All required workgroup participants must reach the corresponding dynamic barrier under target uniformity rules; a converged partial subgroup is insufficient |
| Memory barriers, atomics and shared storage | Prove scope, ordering and race freedom separately; a convergence relation neither flushes memory nor supplies acquire/release |
| Inter-invocation polling/locks | No forward-progress promise; protocols depending on a parked lane's progress are unsupported without a separate target/protocol argument |

For divergent subgroup operations, a future SPIR-V profile additionally requires
the declared relationships, an enforceable execution contract and valid operation
operands. Maximal reconvergence alone relaxes none of the derivative, barrier,
memory or progress obligations above.

Analyze requirements transitively over all reachable callees after recognized
operation calls have been normalized. Unknown effects cannot be silently treated
as scalar. Recursion or a missing body/summary remains unsupported until its
requirements and target legality are established. Uniformity requirements belong
to the relevant scope (workgroup, subgroup or quad), not a universal boolean.

## Diagnostics and capability status

Proposed future diagnostics:

1. reject invalid or unproved relationships at the analysis boundary;
2. reject profile/target capability mismatch before backend emission; and
3. reject an invalid realization after final target generation/validation.

Diagnostics should identify function, original operation/label, relationship,
requested profile and unsupported target requirement. None is implemented by
this slice. The existing WGSL no-finite-exit-path rejection remains unchanged.

| Surface | Scalar baseline | Portable cooperation | Stronger reconvergence |
|---|---|---|---|
| Public CLSL -> Slang -> WGSL | Unchanged | Proposed restrictions above, not implemented by this slice | Not provided; WGSL/WebGPU exposes no equivalent mode |
| Pinned Slang 2025.12.1 -> WGSL | Scalar probe compiles | Wave-count probe rejected; this does not deny native WGSL subgroup support | Attribute accepted but dropped in byte-identical scalar output |
| Pinned Slang -> SPIR-V | Both probes compile and validate | Operation-specific validity still required | Extension/mode emitted and tiny modules validate; no CLSL correspondence or Vulkan runtime |
| Pinned Slang -> GLSL (capability evidence only) | Scalar probe compiles | Wave probe requires suitable target version | Extension/attribute emitted; glslang Vulkan 1.1 translation validates, no device execution |
| HLSL/CUDA source (peripheral evidence only) | Probes compile | Not a CLSL product claim | Annotation adds no visible guarantee to probe output |

See [version-pinned sources, exact inputs, captured outputs and commands](./reconvergence-capabilities.md).
Compiler acceptance, a Slang attribute, or final SPIR-V markers alone do not
prove relationship preservation, target feature negotiation or hardware
execution. No row is hardware-executed. The existing research snapshot's local
version assumption is superseded by the observed `v2025.12.1-nixpkgs`.

## Residual limits

The oracle models only the fixed four-lane fixtures. It assumes declared
relationships, finite event walks and deterministic per-compilation switch
policy. A `Leave` supports one immediate merge/return observation, and activation
counters are derived from each lane's well-matched fixture walk; this is not
a general scheduler for arbitrary independently supplied traces. IDs/arm names
are fixed test symbols, not parsed user input. It does not infer relationships from CIL, validate arbitrary graphs,
model scheduling or memory, establish a general reducible-CFG correspondence,
or support irreducible cooperative input. General inference and future
capability/shape diagnostics remain unresolved and unimplemented.

## Staged implementation after approval

1. Approve or revise this relationship/profile contract and bounded examples.
   General reducible-CFG relationship selection is still an open design decision.
2. Add operation/call effect summaries and portable-target admission checks.
   Keep scalar behavior unchanged; introduce no new cooperative promise yet.
3. Add the dedicated relationship analysis and a checked correspondence through
   Region and Slang target lowering. Extend the oracle only for admitted shapes;
   reject unsupported irreducible, unbounded or ambiguous cooperative inputs.
4. For an optional Vulkan path, separately implement target selection, final
   module validation, feature negotiation and bounded device observations.
   Emitting an execution-mode marker does not complete this stage.

Each is a later independently reviewed slice. Whether to add a Vulkan execution
path, how to select general relationships, and when to accept precise cooperative
shaders remain human decisions; this PR does not authorize them.

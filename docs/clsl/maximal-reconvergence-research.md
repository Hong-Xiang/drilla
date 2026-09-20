# Maximal Reconvergence for a CIL-First Shader Compiler

Research snapshot: 2026-09-14.

**Status: research and proposed direction, not an implemented compiler contract.**
This document accompanies the baseline and pointer-lowering repairs in PR #79.
It does not introduce control-flow restructuring, new shader attributes, subgroup
operations, a Vulkan execution path, or a Slang upgrade.

The later [proposed reconvergence contract](./reconvergence-contract.md) fixes
bounded executable examples and the initial checked-shape policy without changing
production compiler behavior.

The assumed input is compiled .NET CIL, not the original C# AST. The goal is to
define useful, explicit GPU execution semantics for that input, not to reconstruct
every source-level syntactic intention. Optional source annotations may be
considered later; they are not a prerequisite for the direction discussed here.

## 1. Conclusions and scope decision

1. Maximal reconvergence is an execution contract about which invocations
   participate in the same dynamic instruction instance. It is not a general
   arbitrary-CFG-to-structured-code algorithm.
2. Losing the C# AST is not fatal. CLSL can define its own CFG/region-based
   programming model. However, selecting that model, preserving it through
   transformations, and obtaining backend execution guarantees are separate tasks.
3. Immediate post-dominators alone do not define the desired GPU semantics.
   Structurally valid output and per-invocation equivalence are insufficient for
   operations that depend on cooperation between invocations.
4. Current upstream Slang supports requesting maximal reconvergence for SPIR-V.
   Current standardized WGSL/WebGPU does not expose an equivalent guarantee.
   Generating Slang source cannot bridge that gap by itself.
5. Keep the current PR focused on its verified baseline repair. Begin a separate
   control-flow workstream with an explicit semantic contract and adversarial
   tests. Do not postpone the design until after adding cooperative GPU operations,
   but do not promise complete maximal reconvergence in the baseline PR.

Sections describing Khronos rules summarize the cited specifications. Sections
describing a CLSL contract, implementation stages, or effort are engineering
recommendations, not existing functionality or normative claims.

## 2. The original Slang discussion

The starting point is
[shader-slang/slang discussion #8609](https://github.com/shader-slang/slang/discussions/8609),
especially tangent-vector's [initial answer][discussion-initial] and
[follow-up][discussion-follow-up].

### 2.1 Structural validity is different from semantic correctness

The initial answer says that choosing immediate post-dominators as merge points
can produce Slang IR satisfying its structural invariants. Whether the result is
valid for the intended program semantics depends on the programming model exposed
by the translator.

Merge placement becomes observable with cooperative operations: barriers,
shared-memory communication, subgroup/wave operations, and fragment texture
sampling that implicitly computes derivatives. Moving a merge either too early
or too late can change values, prevent progress, or cause pathological behavior.

### 2.2 Why an ordinary CFG can lose an important distinction

Consider two illustrative programs:

```text
// A: work and release occur before leaving the loop.
while true:
    if tryAcquire():
        criticalSection()
        release()
        break
after()
```

```text
// B: leave the loop before doing the work and releasing.
while true:
    if tryAcquire():
        break
criticalSection()
release()
after()
```

Both can have the same ordinary CFG and the same per-invocation instruction
sequence. Yet a programmer may expect A to release the lock before waiting for
other invocations at the loop exit. In an interpretation of B where successful
invocations wait at the exit before executing the release, other invocations
cannot acquire the lock and leave the loop.

This is an illustration, not a portable shader-lock recipe. The discussion
explicitly does not claim that either interpretation always occurs on every API
and device. Maximal reconvergence does not independently guarantee forward
progress or correct memory synchronization.

Once such source structure has been erased, no ordinary dominator/post-dominator
analysis can generally recover which source interpretation was intended.

### 2.3 The two suggested programming models

The recommendation was to provide either:

- Reconvergence based on the source AST's shape and nesting, preserving that
  information through compilation.
- Maximal reconvergence, with an appropriate control-flow restructuring strategy
  when starting from an unstructured CFG.

The answer mentions historical DXC changes that introduced artificial boolean
control variables to preserve structure through LLVM. This is evidence that
otherwise reasonable serial transformations can lose GPU-significant information,
not a recommendation to insert flags indiscriminately.

The follow-up considers it feasible to recover a structured AST whose implied
merge points implement maximal reconvergence for an input CFG. It also explicitly
says the author had not implemented that pass and could not assess its practical
difficulty. Treat this as expert feasibility guidance, not a supplied algorithm
or proof for all CIL programs.

### 2.4 Labeled exits solve representation, not semantics

Slang's multi-level `break` can express useful nested control-flow encodings.
The follow-up corrects the initial reply: labeled `continue` is not supported;
nested loops and labeled breaks can emulate its effect.

The initial answer already warns that such encodings may imply undesired merge
points. On targets without native multi-level breaks, Slang can introduce boolean
variables and conditional control flow. The follow-up warns against assuming
downstream optimization will always recover ideal loops.

Consequently, "the branch can be expressed in Slang" is a weaker statement than
"the chosen structure preserves the required cooperation semantics."

## 3. Terminology and the Khronos execution contract

The primary source is [SPV_KHR_maximal_reconvergence][spirv-mr], revision 2,
last modified 2024-04-18. The [Vulkan proposal][vulkan-proposal] gives explanatory
examples; the SPIR-V extension contains the formal rules.

| Term | Meaning in this discussion |
|---|---|
| Invocation | One shader program instance, not an entire subgroup or workgroup. |
| Dynamic instruction instance | One execution of an instruction in its dynamic calling/loop context. |
| Tangle | Invocations executing the same dynamic instruction instance. |
| Converged for a scope instance | A tangle containing every invocation of that scope instance. |
| Merge | A declared control-flow location with a specified reconvergence relationship. |
| Continue target | A loop location associated with reconvergence of continuing invocations for an iteration. |
| Escape | Leaving a particular pending reconvergence relationship, not necessarily terminating the invocation. |

A tangle is a logical participation set. It does not promise physical lockstep,
equal values, memory visibility, or device-wide synchronization. Several tangles
can be relevant within a subgroup, and being converged for one scope is not
equivalent to being converged for a larger scope.

### 3.1 Allowed divergence

The extension restricts the instructions that introduce divergence. Its rules
cover conditional branches, switches, and specified helper-invocation
demotion/termination situations. Implementations cannot arbitrarily split an
ordinary incoming tangle without regard to those rules.

Conditional branching partitions invocations according to the condition.
Switches have additional implementation latitude described below.

### 3.2 Related dynamic instances determine reconvergence

The following is a practical summary, not a replacement for the normative
definition of related dynamic instances and escaping invocations:

| Location | Relevant earlier execution |
|---|---|
| Selection merge | The related execution of the selection merge instruction. |
| Loop merge | The related loop entry, identified by a merge-instruction execution reached other than through the backedge. |
| Continue target | The related current iteration, identified by the relevant latest loop-merge execution. |
| After a function call | The related dynamic call instance. |
| Switch case/default target | Switch-specific rules with weaker subset guarantees. |

The required participants are the corresponding invocations that have not escaped
that particular reconvergence relationship. Reaching the same static basic block
is not sufficient to establish that two invocations belong to the same instance.

### 3.3 Loops: keep iteration boundaries distinct

Invocations can leave a loop on different iterations. A subgroup operation in a
break path remains associated with its iteration, while an operation at the
declared loop merge can involve invocations that completed different numbers of
iterations.

Likewise, continuing invocations reconverge at the continue target and begin the
next iteration together under the specified relationship. Moving an operation
between the loop body, continue construct, break path, and loop merge can change
its participation semantics.

This is why "maximal" does not mean combine every invocation currently at the same
program counter. The extension restricts reconvergence to merge blocks, continue
targets, case constructs, and after function calls. It also prohibits some
premature combinations, rather than only requiring eventual joins.

### 3.4 Break, continue, and return

An invocation can escape an inner relationship by branching to an enclosing merge
or continue target, returning from the current function, or terminating under the
specified rules.

An invocation executing `break` does not disappear forever. It can leave an inner
relationship and later participate at an enclosing loop merge. A callee return
escapes internal relationships but can be followed by reconvergence after the
call. Returning from a shader entry point does not imply a later caller merge.

### 3.5 A participation-count example

Assume four relevant invocations, numbered 0 through 3, entered this loop together.
Assume suitable subgroup operations and an execution environment enforcing the
stated contract. `activeCount` and `record` below are pseudocode, not CLSL APIs.

```text
for i = 0, 1, 2, ...:
    if i == laneNumber:
        record("before-break", activeCount())
        break

record("after-loop", activeCount())
```

The four before-break executions belong to different iterations and each has one
participant. At the declared loop merge the four surviving invocations reconverge,
so the after-loop operation has four participants.

Moving the first `record` to after the loop would change the observation. This
example is a useful future test because it reveals participation errors without
relying on a shader hanging or a watchdog timeout.

The assumption of four participating invocations is part of the example, not a
portable guarantee about hardware subgroup size or launch layout.

## 4. Restrictions and non-guarantees

### 4.1 Switch is not universally deterministic across implementations

Equal selectors within an incoming tangle remain together. Different selectors
that enter the same case construct may or may not be grouped together. The
extension fixes that choice for a given compilation, not across all compilers,
devices, or driver versions.

The Vulkan proposal recommends `if`/`else` when stronger guarantees are required.
A strict CLSL profile might restrict such switches or choose a controlled
lowering, but it would need to inspect final SPIR-V: generating `if` in Slang
does not prove downstream optimization cannot produce `OpSwitch`.

### 4.2 Additional structural requirements

For functions in the static call tree of an annotated entry point, blocks with
multiple unique predecessors must be loop headers containing `OpLoopMerge`,
declared merge blocks, declared continue targets, or switch targets/defaults.
Conditional branch true and false targets must differ under this execution mode.

Ordinary structured shader control-flow requirements still apply. The extension
does not authorize arbitrary irreducible CFGs. Irreducible CIL input needs a
specified transformation or explicit rejection.

### 4.3 Reconvergence is not synchronization

It does not replace:

- Acquire/release semantics or memory barriers.
- Legal participation in a workgroup control barrier.
- Correct atomic protocols and data-race handling.
- Quad participation and validity requirements for derivatives.

A partially active subgroup with a well-defined tangle is not automatically a
complete workgroup or a valid derivative quad.

### 4.4 Reconvergence is not a forward-progress guarantee

The extension's forward-progress discussion states that behavior is undefined
when invocations do not make progress. Arbitrary cross-lane or cross-workgroup
spinlocks are not made safe merely by enabling the extension.

### 4.5 Defined participation is not whole-program determinism

The desired CLSL property is an explicit participation contract under stated
conditions. It does not imply deterministic subgroup membership across devices,
inter-subgroup atomic ordering, scheduling, resource allocation order, or
bitwise-identical floating-point reductions.

## 5. What a CIL-first compiler must choose

### 5.1 There is no universal unique answer for every bare CFG

The Khronos definition depends on structured SPIR-V information. It does not
specify how to infer that information uniquely from CIL.

LLVM's [Convergence and Uniformity][llvm-uniformity] documentation also notes that
different cycle hierarchies for an irreducible CFG can produce different maximal
converged-with relations. LLVM's model and the Khronos extension are related
material, not interchangeable definitions without an explicit mapping argument.

CLSL must therefore select and document a normalization/region policy. It should
not claim to recover an unavailable source AST.

### 5.2 Proposed semantic boundary

```text
CIL
  -> normalized CFG
  -> explicit cycle/region and convergence relationships
  -> transformations preserving those relationships
  -> structured IR
  -> Slang source
  -> target validation and execution
```

This is a proposed architecture, not the current pipeline's guarantee.

For the same input and compiler profile, region choices must not depend on
dictionary enumeration or accidental graph traversal order. Renaming labels or
reordering an input block collection should not accidentally change participation.

Different C# compiler versions or optimization settings may produce different
CIL. Unless additional information is supplied, CLSL need not promise that such
different inputs preserve the same original source-level cooperative intention.
Any normalization before the semantic boundary must itself be documented.

Once region relationships are established, later passes must preserve them rather
than silently recompute semantics from a changed CFG.

### 5.3 Proposed support tiers

| Tier | Proposed policy |
|---|---|
| Per-invocation computation | Preserve supported CIL values, effects, control flow, and termination; allow broader structurization techniques where participation is not observable. |
| Cooperative operations under portable conditions | Require the relevant target uniformity and validity rules; propagate requirements through calls. |
| Divergent cooperative operations with precise participation | Require a defined region contract and a backend that can enforce it. |
| Unsupported irreducible or otherwise unproven cases | Diagnose explicitly instead of silently weakening guarantees. |

Node splitting or dispatcher loops are not inherently forbidden. They introduce
extra proof obligations: cloning changes static operation sites, and dispatcher
loops introduce artificial dynamic iteration boundaries. Ordinary flowchart
equivalence is not sufficient evidence for cooperative semantics.

### 5.4 Relationship to the existing fold/algebra design

Keep structural traversal and graph semantics separate:

- Folds/algebras collect operations, region facts, and effect requirements.
- Dedicated graph analyses compute cycles, relationships, and fixed points.
- Typed analysis results describe permitted transformations.
- Maps and structured interpreters perform rewriting and emission.

Future region types should make loop body, continue, merge, and exit targets
explicit. Convergence-sensitive operations and transitively called helpers need
effect/participation information. LLVM convergence tokens are useful inspiration,
but importing that entire mechanism is not a prerequisite or an approved design.

## 6. Backend feasibility

### 6.1 Vulkan / SPIR-V

The Vulkan extension requires device support and enabling
`VK_KHR_shader_maximal_reconvergence`, querying and enabling
`VkPhysicalDeviceShaderMaximalReconvergenceFeaturesKHR.shaderMaximalReconvergence`,
and emitting the corresponding SPIR-V extension and execution mode:

```text
OpExtension "SPV_KHR_maximal_reconvergence"
OpExecutionMode %entry MaximallyReconvergesKHR
```

The SPIR-V extension adds an execution mode, not a new
`OpCapability MaximalReconvergenceKHR`. Its enabling capability is `Shader`.
Slang's similarly named compiler capability atoms are a separate concept.

The Vulkan extension has a Vulkan 1.1 dependency. Current Vulkan rules also forbid
this execution mode when an invocation repack instruction is statically used
(`StandaloneSpirv-MaximallyReconvergesKHR-09565`).

Final-module validation is required, including the entry-point call tree, not
just the top-level function. A valid module does not itself prove that CLSL chose
the intended regions.

### 6.2 Subgroup uniform control flow is weaker

`SPV_KHR_subgroup_uniform_control_flow` supplies guarantees for structured control
flow entered uniformly by a subgroup, with its specified merge conditions.
Maximal reconvergence additionally provides useful relationships for partial
tangles, escapes, and loop iterations.

For example, the Vulkan proposal describes election, broadcast, and bit counting
inside a divergent selection operating on the same tangle. Requiring full-subgroup
uniform entry everywhere is a more restrictive programming model.

### 6.3 Slang support observed upstream

Research inspected Slang commit
`d3a113484c9980970d76633e6a57cfd1a7631a3e` on 2026-09-14.
The public attribute is:

```slang
[MaximallyReconverges]
```

The [core declaration][slang-attribute], [IR lowering][slang-lowering],
[SPIR-V emitter][slang-spirv], and [upstream test][slang-test] establish that Slang
can request the SPIR-V execution mode.

The attribute comment describes SPIR-V support and says other targets are
unaffected. Current [GLSL emission][slang-glsl] is more specific: it also supports
`GL_EXT_maximal_reconvergence` and the corresponding GLSL attribute. This does not
create WGSL support.

An internal `__requireMaximallyReconverges` marker also exists, but the public
entry-point attribute is the clearer starting point for a transpiler.

Not verified by this research:

- The earliest released Slang version containing these paths.
- Equivalent behavior in the locally used Slang 2025.23.2.
- Actual local device support.
- Preservation of our proposed region semantics through every Slang optimization.

No Slang upgrade or attribute plumbing is included in the current PR.

### 6.4 WGSL / WebGPU

The inspected GPUWeb snapshot is
`e0aff163a37eb3633ffd612e2a943ceb6196d6af`; findings reflect the specifications
available on 2026-09-14, not a claim about every browser's implementation.

The standardized WebGPU feature set does not expose maximal reconvergence, and
shader-module creation uses WGSL rather than application-supplied SPIR-V.
A browser's internal Vulkan backend is not a way for the application to request
this Vulkan execution mode. See the [WebGPU source snapshot][webgpu].

[WGSL][wgsl] warns that subgroup operations under non-uniform control flow may
have an active invocation set different from what the author expects. The
`subgroup_uniformity` language extension adds subgroup-scoped uniformity analysis;
it is not Khronos-style maximal reconvergence for arbitrary divergent regions.
Changing diagnostic severity does not supply missing execution guarantees.

Compute barriers have uniform-control-flow requirements, and derivatives under
non-uniform control flow produce indeterminate values under the specified rules.

**Consequence:** Slang-to-WGSL cannot currently provide a portable promise of the
full Khronos maximal-reconvergence contract. This is a target-contract limitation,
not simply a missing emitter optimization.

A useful near-term WGSL profile can still support ordinary control flow and
cooperative operations under appropriately checked portable conditions. Adding a
Vulkan profile later is an option, not a decision to abandon the current WGSL path.

## 7. Relevant algorithms and implementation references

### Beyond Relooper

Norman Ramsey, *Beyond Relooper: Recursive Translation of Unstructured Control
Flow to Structured Control Flow*, ICFP 2022:
[paper][ramsey], [DOI](https://doi.org/10.1145/3547621).

The central translation handles reducible CFGs using dominator-tree and
reverse-postorder information. The paper describes GHC handling irreducible input
through node splitting first. It is a strong starting point for nested structured
control flow and multi-level exits, but not a proof of GPU participation
preservation. Node splitting can duplicate cooperative operation sites.

### Earlier structured-control-flow work

Peterson, Kasami, and Tokura, *On the Capabilities of While, Repeat, and Exit
Statements* (1973), [DOI](https://doi.org/10.1145/355609.362337).

This is the historical work tangent-vector highlighted. It establishes important
structured-flow expressiveness results; it does not independently settle modern
GPU convergence semantics.

### LLVM convergence semantics and structurization

[Convergent Operation Semantics][llvm-convergent] describes entry, loop, and
anchor tokens, including dynamic iteration identity and extended cycles. Extended
cycles are particularly relevant to cooperative operations in break paths outside
the natural CFG cycle.

[Convergence and Uniformity][llvm-uniformity] explains CFG-based reasoning,
including the cycle-hierarchy ambiguity for irreducible graphs.

LLVM's [SPIR-V structurizer][llvm-structurizer] uses convergence-region analysis.
It is implementation material worth studying, not a drop-in C#-CFG-to-Slang pass.
No complete arbitrary-CIL-to-Slang construction proven against the Khronos
extension was established by this research.

### Uniformity analysis

Rosemann, Moll, and Hack, *An Abstract Interpretation for SPMD Divergence on
Reducible Control Flow Graphs*, POPL 2021,
[DOI](https://doi.org/10.1145/3434312).

This is relevant to proving uniformity and identifying safe transformations.
Uniformity analysis and maximal reconvergence are related but different tasks.

## 8. Current repository position

Snapshot: baseline implementation at commit `9fd24f0`, before this research-only
documentation addition.

| Surface | Observed role or limitation |
|---|---|
| `DualDrill.CLSL.Language/Analysis/ControlFlowAnalysis.cs` | Builds DFS, dominator, and post-dominator information; exposes a loop-header query, not an explicit convergence model. |
| `DualDrill.CLSL.Language/Region/RegionTree.cs` | `Create` builds regions from the dominator tree and loop classification, passing null next/break targets. |
| Historical `PostDominatorAnalysis` (removed after this snapshot) | Contained loop-aware merge heuristics; these were not a specified maximal-reconvergence contract. |
| `DualDrill.ILSL/Backend/SlangEmitter.cs` | Uses immediate post-dominators and target stacks, with duplicate target expansion in `EmitBranch`. |
| `DualDrill.ILSL/SlangService.cs` | Invokes Slang CLI; the current compilation path emits WGSL. |
| `DualDrill.CLSL.Test/StructuredControlFlowTests.cs` | Tests representative region shapes, not multi-invocation dynamic participation. |
| `DualDrill.CLSL.Language/Transform/RegionParameterToLocalVariablePass.cs` | Repairs stable-address parameter lowering; does not implement reconvergence. |

The baseline repair adds 25 direct IR regression cases to the existing 69 tests.
The final implementation verification before this documentation addition was:

```text
dotnet test --no-restore --logger "console;verbosity=normal" --verbosity quiet

Test Run Successful.
Total tests: 94
     Passed: 94
 Total time: 4.7111 Seconds
```

These results establish the tested baseline, not general control-flow correctness,
GPU execution equivalence, or maximal reconvergence. Existing dependency/compiler
warnings remain. The pointer pass explicitly rejects unsupported pointer sources
and differing conditional value arguments to a shared target rather than silently
miscompiling them.

## 9. Proposed implementation slices

These are future work, not changes included by adding this document.

### Slice A: semantic contract and counterexamples

Specify the CIL normalization boundary, supported reducible shapes, escape rules,
operation effects, and backend-specific promise. Record known ambiguities rather
than assuming original source recovery.

Establish tests that distinguish a break-path operation from a post-loop
operation, and a current-iteration operation from a later-iteration operation.

### Slice B: explicit structured IR and reliable ordinary control flow

Represent loop, continue, merge, and enclosing exit targets explicitly. Replace
emitter-time guessing and repeated block expansion with interpretation of the
structured representation.

Keep the existing fold/algebra style. Graph analysis determines the regions;
emission does not make new semantic decisions.

Start with reducible CFGs. Any fallback for broader private computation must have
a separately stated contract for convergence-sensitive operations.

### Slice C: participation reference model

Build a small multi-invocation model for the chosen IR contract, not a complete GPU
simulator. Track dynamic region/iteration instances and model ballot/count-style
observations.

A single-invocation CIL or ECMA IR interpreter remains valuable for ordinary
control flow and numeric behavior, but cannot alone establish collective
participation, barrier legality, or memory visibility.

### Slice D: backend-specific enforcement

For WGSL, enforce the supported portable conditions and use actual target
validation. Do not suppress uniformity diagnostics to advertise broader support.

For an optional Vulkan profile, verify Slang execution-mode emission, feature
negotiation, final SPIR-V structure, and execution on supporting devices.
This does not require changing the current project's backend immediately.

### Slice E: broader semantics

Only after the basic contract is demonstrated, consider irreducible CFGs, richer
switch behavior, helper invocations, broader optimizations, and optional source
markers. These should remain independently reviewable changes.

## 10. Future validation matrix

| Case | Required observation |
|---|---|
| Divergent diamond | Non-escaping participants reunite at the declared merge. |
| Selection entered by a partial subgroup | Inner cooperative operations use the intended partial tangle, not an assumed full subgroup. |
| Different loop exit iterations | Break-path observations stay iteration-specific; loop-merge observations combine the appropriate survivors. |
| Multiple continue paths | Continuing invocations meet at the correct iteration boundary. |
| Nested loops and multi-level exits | Escapes affect the proper inner relationships without losing enclosing ones. |
| Early callee returns | Internal relations end appropriately; surviving call participants rejoin after the call. |
| Entry-point return | Terminated invocations are not incorrectly expected at later operations. |
| Switch shared targets/fallthrough | Apply an explicit policy for implementation-dependent participation. |
| Irreducible cooperative CFG | Reject until a specific semantics-preserving strategy is established. |
| Label renaming and block reordering | Do not accidentally change the declared participation relationships. |
| Operation movement or duplication | Detect altered cooperation when crossing body/continue/break/merge boundaries. |
| Final SPIR-V | Check extension, execution mode, merges, continue targets, and the entry-point call tree. |
| Actual execution | Compare ballot/count observations to the declared reference model on supporting implementations. |

Use `spirv-val` with the intended Vulkan environment where applicable. Passing
structural validation is necessary but does not prove semantic equivalence.
Runtime tests must avoid assuming a fixed subgroup size and should prefer bounded
observations over intentionally deadlocking shaders.

## 11. Work estimate and sequencing

These are rough planning estimates for one experienced compiler engineer familiar
with the repository. They are not measured tasks or commitments; the categories
overlap and should not be summed mechanically.

| Scope | Indicative effort | Main uncertainty |
|---|---|---|
| Contract, counterexamples, local Slang capability experiment | Several days to about 2 weeks | Pinning exact backend/version behavior. |
| Robust reducible-CFG structure and explicit exits | Several weeks | Current emitter assumptions and difficult exit shapes. |
| Explicit convergence regions, dynamic-instance model, adversarial tests | Overall 4-8+ weeks | Correctness across loops, escapes, and transformations. |
| Vulkan feature negotiation, validation, runtime harness | Additional 1-3 weeks if a usable Vulkan path/device already exists | Backend setup and hardware availability. |
| Broader optimizations, helper behavior, complex calls and switches | Additional weeks to months | Preserving a contract across the full pipeline. |
| General irreducible cooperative CFG support | Research-sized, months; no reliable upper bound yet | Choice of hierarchy/normalization and proof obligations. |
| Full portable Khronos semantics through standardized WGSL | Not just an implementation-effort question | Missing target execution contract. |

The hard part is not printing labeled breaks. It is preserving the correspondence
between cooperative operations and their dynamic participant sets.

## 12. PR boundary and outstanding questions

The current PR can be evaluated as a baseline/pointer-lowering repair plus research
documentation. Full reconvergence implementation is not a prerequisite for that
scope. Normal review, current checks, and merge-conflict status still apply;
this document is not a standing certification of merge readiness.

Before implementing the next phase, resolve:

- Which exact normalized-CIL region policy defines CLSL participation semantics?
- Which supported shapes can be translated without losing those relationships?
- How should break-path membership be represented when the natural CFG cycle does
  not contain the path?
- Which convergence-sensitive operations are permitted by the portable WGSL profile?
- Which local Slang version and target device capabilities will be tested?
- How will transformations preserve region identities and call relationships?
- What evidence is required before accepting irreducible cooperative input?

Known limits of this research:

- Upstream attribute support was inspected; local end-to-end maximal reconvergence
  was not exercised.
- Browser/driver availability and private native extensions were not surveyed.
- No general arbitrary-CFG-to-Slang proof was found or established.
- LLVM and Khronos models still need an explicit correspondence if both are used.
- The proposed CLSL semantic policy is not fully specified or implemented.
- No original-C#-AST equivalence, global scheduling determinism, or numerical
  bitwise determinism is promised.

## 13. Sources

Primary specifications and implementation snapshots:

- [SPV_KHR_maximal_reconvergence][spirv-mr]: normative definitions, structural
  requirements, switch latitude, and forward-progress discussion.
- [VK_KHR_shader_maximal_reconvergence proposal][vulkan-proposal]: motivation,
  resource-selection and atomic-compaction examples.
- [Vulkan feature reference][vulkan-feature]: device feature query/enable structure.
- [SPV_KHR_subgroup_uniform_control_flow][spirv-sucf]: the weaker related guarantee.
- [Slang public attribute][slang-attribute], [IR lowering][slang-lowering],
  [SPIR-V emission][slang-spirv], [test][slang-test], and [GLSL emission][slang-glsl].
- [WebGPU snapshot][webgpu] and [WGSL snapshot][wgsl].

Discussion and algorithm references:

- [tangent-vector's initial answer][discussion-initial] and
  [follow-up][discussion-follow-up].
- [A Digression on Divergence][divergence-blog] (2013): historical explanation,
  not a current hardware or API specification.
- [Beyond Relooper][ramsey].
- [LLVM Convergent Operation Semantics][llvm-convergent],
  [Convergence and Uniformity][llvm-uniformity], and
  [SPIR-V structurizer][llvm-structurizer].

[discussion-initial]: https://github.com/shader-slang/slang/discussions/8609#discussioncomment-14618078
[discussion-follow-up]: https://github.com/shader-slang/slang/discussions/8609#discussioncomment-14651305
[spirv-mr]: https://github.com/KhronosGroup/SPIRV-Registry/blob/3363c835cf5f1665082499ee31f8670b4201bb3f/extensions/KHR/SPV_KHR_maximal_reconvergence.asciidoc
[vulkan-proposal]: https://github.com/KhronosGroup/Vulkan-Docs/blob/f84d432d5b8912362f96f581f29bbc4f3c8c7843/proposals/VK_KHR_shader_maximal_reconvergence.adoc
[vulkan-feature]: https://docs.vulkan.org/refpages/latest/refpages/source/VkPhysicalDeviceShaderMaximalReconvergenceFeaturesKHR.html
[spirv-sucf]: https://github.com/KhronosGroup/SPIRV-Registry/blob/main/extensions/KHR/SPV_KHR_subgroup_uniform_control_flow.asciidoc
[slang-attribute]: https://github.com/shader-slang/slang/blob/d3a113484c9980970d76633e6a57cfd1a7631a3e/source/slang/core.meta.slang#L4992-L4995
[slang-lowering]: https://github.com/shader-slang/slang/blob/d3a113484c9980970d76633e6a57cfd1a7631a3e/source/slang/slang-lower-to-ir.cpp#L14718-L14720
[slang-spirv]: https://github.com/shader-slang/slang/blob/d3a113484c9980970d76633e6a57cfd1a7631a3e/source/slang/slang-emit-spirv.cpp#L6625-L6628
[slang-test]: https://github.com/shader-slang/slang/blob/d3a113484c9980970d76633e6a57cfd1a7631a3e/tests/hlsl-intrinsic/quad-control/quad-control-frag-many-entry-points.slang#L52-L60
[slang-glsl]: https://github.com/shader-slang/slang/blob/d3a113484c9980970d76633e6a57cfd1a7631a3e/source/slang/slang-emit-glsl.cpp#L1566-L1570
[webgpu]: https://github.com/gpuweb/gpuweb/blob/e0aff163a37eb3633ffd612e2a943ceb6196d6af/spec/index.bs
[wgsl]: https://github.com/gpuweb/gpuweb/blob/e0aff163a37eb3633ffd612e2a943ceb6196d6af/wgsl/index.bs
[divergence-blog]: https://tangentvector.wordpress.com/2013/04/12/a-digression-on-divergence/
[ramsey]: https://www.cs.tufts.edu/~nr/pubs/relooper.pdf
[llvm-convergent]: https://llvm.org/docs/ConvergentOperations.html
[llvm-uniformity]: https://llvm.org/docs/ConvergenceAndUniformity.html
[llvm-structurizer]: https://github.com/llvm/llvm-project/blob/main/llvm/lib/Target/SPIRV/SPIRVStructurizer.cpp

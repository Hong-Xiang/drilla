# Bounded control-flow corpus

This test-only corpus exercises the existing compiler, not an alternative
structurizer. It extends the scalar regressions with typed raw value CFGs,
computed control facts, checked regions, target ASTs and emitted-source
execution models. Ordinary C# fixtures separately exercise actual Debug/Release
CIL and the public `CLSLCompiler.Parse/Compile/Emit` boundary.

Implementation: `DualDrill.CLSL.Test/ControlFlowCorpus{,Tests}.cs`.
The existing helper extensions are typed raw-graph and annotated-facts entry points in
`ScalarControlFlowOracle`; its interpreter is reused.

## Quantified coverage

| Family | Executable witness | Bound / expectation |
|---|---|---|
| Forward diamonds, nested conditions, shared tails | `ExhaustiveForwardGraphsReachAllOrderedArms` | All reachable ordered forward graphs on 1-4 nodes: 1/2/10/80 graphs, 93 total; return/jump/binary branch only; at most 6 ordered edges |
| Crossing/staggered joins | `NamedCrossingAndStaggeredJoinTopologiesRemainInTheExhaustiveCorpus` | `forward-4-0118 = 1,2;2,3;3;R`, `forward-4-0139 = 1,3;2,3;3;R`; executed by the exhaustive test |
| Larger forward graphs | `SeededLargerForwardGraphsReachAllOrderedArms` | 64 unique samples, 5-12 nodes, at most two ordered successors per node |
| Ordered-arm coverage | Both forward tests | A reachability witness for every reachable `(source, arm-index)`; not just all-true/all-false inputs |
| Label renaming / input-storage reversal | `LabelRenamingAndDefinitionReorderingPreserveIdentitySemantics` | Three variants of sample 17, same explicit stable node mapping and every-arm inputs |
| Same-target distinct values | `SameTargetOrderedArmsRetainSelectedValues` | True -> 11, false -> 29; two incoming arms from the same source, indices 0/1 |
| Parallel backedge copies | `SelectedBackedgeSwapsInParallel` | `(a,b)=(1,3)`; 0/1/4 swaps -> 13/31/13; trace lengths 2/3/6 |
| Multiple latches and exits | `NaturalLoopHasTwoLatchesAndTwoObservableExits` | Two backedge sources and two actual natural-loop exit sources; true/3 -> 13 in 11 blocks, false/3 -> 23 in 13 |
| Deep nesting | `OneTripLoopChainsHaveActualComputedDepth` | Computed loop-ancestor depth 4/8/16/32; innermost increment returns 1; budget `64*depth+64` |
| Irreducible rejection | `IrreducibleTwoEntryAndSideEntryCyclesAreRejectedStructurally` | Two-entry 3-node cycle and 5-node side-entry graph rejected by lexical scope checking |
| Ordinary C# public path | `OrdinarySharpShaderUsesPublicParseCompileAndEmitBoundaries` | `OrdinaryControl(0)=17`, `(3)=21`; value/facts/region/scoped/emitted-model traces agree; native Slang and public WGSL products |
| No-exit / mixed return-divergence | `DivergenceCorpusSeparatesSlangAndWgslCapabilities` | Mixed returning input -> 7; no-exit IR/Slang emission; explicit WGSL restriction; nonreturning inputs exhaust a 100-step model budget |
| Native dense switch | `DenseSwitchRunsThroughSharedStagesAndPublicTargets` | 12 inputs; real CIL switch in both builds, ordered shared-IR arms 0..8 preserved; CPU/value/facts/region/scoped/emitted-model agreement and public IR/Slang/WGSL |
| Same-target multiway values | `SameTargetSwitchPreservesThreeOrderedArmsAndTuples` | Six inputs; cases 0/1/default -> 11/29/41 at one join; incoming arms 0/1/2 remain distinct |
| Multiway loop repeats and exits | `LoopDispatchSwitchPreservesRepeatAndExitArmIdentity` | Four-node raw CFG, nine input pairs; dispatch arms 0/1 repeat with different parallel tuples, case 2/default arms 2/3 exit to one label with different values |
| Native target syntax | `RepresentativeGeneratedAndHandSourcesPassNativeSlangValidation` | Crossing-forward and multiple-latch/exit sources passed to existing `SlangService.ValidateAsync` |

The larger generator uses unsigned 32-bit arithmetic:
`state = state * 1664525 + 1013904223 (mod 2^32)`, seed `20260920`,
then `state % exclusiveMaximum`. It first adds reachable forward parent edges,
then optional forward edges; duplicate encodings are discarded. Input captures
retain every sampled encoding. These samples are not uniform random graphs.
The exhaustive family is **not** all four-node directed graphs: edges only
target later nodes, so it contains no cycles.

Existing `ScalarControlFlowTests` is deliberately reused rather than copied:
`ScalarCilMatchesCpuAndEmittedControl`, `NestedBreakAndContinueMatchCpuAndEmittedControl`,
`NestedEarlyReturnMatchesCpuAndEmittedControl` and
`ThreeNestedLoopsMatchCpuAndEmittedControl` cover ordinary C# zero/one/many
iterations, shared tails, nested continue/break/early return and loop state.
`OracleNegativeControlsDetectDuplicateSkipAndWrongSelectedEdge`,
`CfgEdgeArgumentsAreParallelCopiesAndLegitimateRevisitsRemainInTrace` and
`UnsupportedOperationsAndBothExecutionBudgetsFailExplicitly` retain negative
controls, strict types and budget/unsupported-operation failures.
Accepted `CilSwitchTests` additionally supplies empty tables, retained CIL
stack values, nested conditions/continue/break/early return, malformed target
and selector-type controls. Those tests are retained, not duplicated or weakened.

The switch extension is bounded to two new hand topologies and 12 native dense
inputs: it does not extend the forward graph enumerator to arbitrary multiway
graphs. Dense inputs 0..7 return 11/13/17/19/23/29/31/37; signed extrema, -1
and 8 return 41. Same-target inputs are signed extrema, -1, 0, 1 and 2.
The loop matrix covers zero iterations, one/four parallel swaps, three
increments, an explicit case exit and default exits at -1, 3 and both extrema.

## What is compared

For generated graphs a small host calculation over the input adjacency
encoding supplies a golden sum of visited node contributions and a node trace.
The typed raw CFG runs **before** facts and region construction. Subsequent
flat-region, scoped-continuation, normalized-region and emitted-source-model
executions must match results and traces. Hand cases additionally have fixed
numeric goldens and topology assertions.
Both hand switch matrices also compare the annotated-facts CFG directly.
Their entire raw-input matrices execute before region construction. They retain
an explicit `Terminator.D.Switch` in checked shared regions; only target
lowering chooses binary target syntax.

The CFG and scoped models share scalar `Machine` operations; the source model
also shares scalar arithmetic helpers. They are independent **control-transfer
models**, not fully independent arithmetic implementations. Effects are
observable scalar state and block-visit multiplicity, not a general memory,
resource, call-effect or GPU participation trace.

Each original label has a stable node identity. Within a hand/C# instance,
exact label traces are compared; generated/metamorphic cases resolve every
observed original label through an explicit label-to-node map. Renaming does
not compare target text. Input dictionary reversal demonstrates pre-analysis
invariance only: annotation rebuilds the graph in reachable RPO.

Default model budget is 10,000 steps, with the explicit depth/divergence bounds
above. Flat/scoped models count their own execution events; the source model
counts parsed statements/loop activity. Equal limits do not denote equal work.
Exhaustion is an unresolved execution, **not** semantic equivalence or proof
of nontermination. No model executes GPU code.

## Reproduction and captures

Use the pinned Nix environment, in each real configuration:

```sh
nix develop --builders '' --command dotnet test \
  DualDrill.CLSL.Test/DualDrill.CLSL.Test.csproj --no-restore -c Debug \
  --filter 'FullyQualifiedName~ControlFlowCorpusTests|FullyQualifiedName~ScalarControlFlowTests|FullyQualifiedName~CilSwitchTests'
nix develop --builders '' --command dotnet test \
  DualDrill.CLSL.Test/DualDrill.CLSL.Test.csproj --no-restore -c Release \
  --filter 'FullyQualifiedName~ControlFlowCorpusTests|FullyQualifiedName~ScalarControlFlowTests|FullyQualifiedName~CilSwitchTests'
```

Restore that project only if this fails for missing assets. For auditable
captures, start with a clean committed worktree and use a new output directory:

```sh
test -z "$(git status --porcelain)" || exit 1
sha="$(git rev-parse HEAD)"
capture_root="$(mktemp -d)"
for configuration in Debug Release; do
  command="nix develop --builders '' --command dotnet test DualDrill.CLSL.Test/DualDrill.CLSL.Test.csproj --no-restore -c $configuration --filter FullyQualifiedName~ControlFlowCorpusTests"
  DRILLA_E2_CAPTURE_DIR="$capture_root/$sha/$configuration" \
  DRILLA_E2_SOURCE_SHA="$sha" \
  DRILLA_E2_SOURCE_SHA_STATUS=committed-clean \
  DRILLA_E2_REPRO_COMMAND="$command" \
  nix develop --builders '' --command dotnet test \
    DualDrill.CLSL.Test/DualDrill.CLSL.Test.csproj --no-restore -c "$configuration" \
    --filter 'FullyQualifiedName~ControlFlowCorpusTests'
done
```

Captures retain typed input, identity mapping, real computed facts, regions,
target AST/source, executions, diagnostics and command/configuration/SHA metadata.
The public C# capture includes numbered raw CIL through target-product stages.
Rejected inputs retain available earlier stages, not invented later output.
Generated-case failures are aggregated with their case encoding and first
failing stage, then fail the test: they are not skipped or marked expected.
Minimization remains a deliberate investigation after a genuine failure,
not a claim attached to the first failing enumeration entry.

## Captured input/output example

The following is **actual captured output**, not proposed IR. `hand-same-target`
starts at the typed value-CFG boundary; it was not produced from C#:

```text
function i32 SameTarget11Or29(bool choose)
locals=[]
flat-value-cfg
^0:same-entry parameters=[]
    %0 = load(&arg(choose))
    control: br_if %0 true=^1(same-exit)(11_i32) false=^1(same-exit)(29_i32)
^1:same-exit parameters=[%4]
    control: return %4
```

Its computed join facts preserve the parallel arms:

```text
^1:same-exit parameters=[%4] facts={rpo=1 idom=^0:same-entry postdom=function-exit(finite) incoming=[^0:same-entry[0]:forward,^0:same-entry[1]:forward] loop-header=false}
```

The actual checked scope resolves both arms separately:

```text
scope ^0(same-entry): [^1(same-exit) forward owner=^0(same-entry)]
scope ^1(same-exit): []
transfer ^0(same-entry)#0 -> ^1(same-exit) forward owner=^0(same-entry)
transfer ^0(same-entry)#1 -> ^1(same-exit) forward owner=^0(same-entry)
```

The actual target AST contains selected-edge copies, not an unconditional phi:

```text
if %2
    then
        bind %3 : i32 = load (11_i32)
        assign %0(parameter_0) <- %3
        assign %1(control) <- 0_i32
        break
    else
        bind %4 : i32 = load (29_i32)
        assign %0(parameter_0) <- %4
        assign %1(control) <- 0_i32
        break
```

Actual execution for true (false has the same trace and result 29):

```text
arguments=[Boolean { Data = True }]
raw result=Integer { Data = 11 } trace=0 -> 1
region result=Integer { Data = 11 } trace=0 -> 1
scoped result=Integer { Data = 11 } trace=0 -> 1
normalized result=Integer { Data = 11 } trace=0 -> 1
emitted result=Integer { Data = 11 } trace=0 -> 1
```

### Captured explicit-multiway extension

After normal merge of accepted dependency
`e8ef765fe4ba4aea894b4e677058f7b018f08608` (tree
`20ea6b4d67e16508b4ca4736e15d8719b6259d0e`), the same-target switch has
this actual value-CFG control and computed incoming arms:

```text
control: switch %0 cases=[^1(switch-same-join)(11_i32), ^1(switch-same-join)(29_i32)] default=^1(switch-same-join)(41_i32)
incoming=[^0:switch-same-entry[0]:forward,^0:switch-same-entry[1]:forward,^0:switch-same-entry[2]:forward]
```

All six inputs have the fixed original trace `entry -> join`, but case 0,
case 1 and default supply distinct results 11, 29 and 41.

The switch-loop state begins `(count,1,3)`. Its dispatch selects a parallel swap,
an increment, an unchanged exit tuple, or a swapped default-exit tuple.
Node identities are `0=entry`, `1=header`, `2=dispatch`, `3=exit`.
Actual results agree in raw/facts/region/scoped/normalized/emitted models:

| `(selector,count)` | Result | Fixed trace |
|---|---|---|
| `(int.MinValue,0)` | 13 | `0 -> 1 -> 3` |
| `(0,1)` | 31 | `0 -> 1 -> 2 -> 1 -> 3` |
| `(0,4)` | 13 | `0 -> 1 -> 2 -> 1 -> 2 -> 1 -> 2 -> 1 -> 2 -> 1 -> 3` |
| `(1,3)` | 43 | `0 -> 1 -> 2 -> 1 -> 2 -> 1 -> 2 -> 1 -> 3` |
| `(2,3)` | 13 | `0 -> 1 -> 2 -> 3` |
| `(-1,3)`, `(3,3)`, `(int.MinValue,3)`, `(int.MaxValue,3)` | 31 | `0 -> 1 -> 2 -> 3` |

Incoming assertions filter by dispatch source: other entry/header edges also
have arm indices 0/1. Checked control resolves dispatch arms 0/1 as `Repeat`
and 2/3 as `Forward`, without collapsing equal labels or argument tuples.

## Counterexample and capability ledger

| Observation | Classification | Status / next boundary |
|---|---|---|
| No unexpected wrong value/trace or reducible rejection in the specified bounded runs | Correct accepted scalar-model behavior | Finite evidence only; no general completeness claim |
| Two-entry / side-entry rejection | Intended out-of-contract rejection | No node-splitting repair implemented |
| Dense switch and two raw multiway topologies accepted | Correct accepted scalar-model behavior | Accepted #158 shared API; ordered cases and default retained; bounded inputs above |
| Reachable `NoExitPath` rejected for WGSL | Target capability restriction | IR/Slang emission remains a separate observation |
| Nonreturning model inputs exhaust budget | Oracle budget / unresolved execution | Neither successful equivalence nor a divergence theorem |

No genuine compiler counterexample required shrinking in these runs. Earlier
personal-review findings corrected **the tests and evidence** (empty facts
captures, an outside-loop branch mislabelled as two exits, disconnected C#
goldens); those were not production compiler fixes.
The reviewed historical baseline
`5117316098b5f1124fed8423ecbd39cd58c90840` and its original captures remain
unchanged: switch was frontend-unsupported at that head. The refreshed corpus
replaces only that obsolete rejection with acceptance after the approved
dependency merge; it does not retroactively relabel historical evidence.

## Beyond Relooper comparison and limits

[Upstream provenance](control-flow-corpus-provenance.md) records exact revisions,
paths, licenses and independently reauthored topology ideas.

The current pipeline computes dominance/RPO and ordered backedges, constructs
dominator-organized regions and checks lexical `Forward`/`Repeat` ownership.
Target lowering realizes those continuations before syntax-only emission.
These are important ingredients shared with Beyond Relooper, not a claim of
algorithmic identity or completed implementation.

General correspondence still needs an argument that every in-contract
reducible CFG obtains legal scoped targets, preserves simultaneous selected-edge
bindings and dynamic effects/returns/divergence, and has appropriate target-size
and resource bounds. Testing depth 32 is not an unbounded recursion guarantee.
The official Beyond Relooper arbitrary-CFG pipeline separately repairs
irreducibility by node splitting; this compiler currently rejects it instead.

Switch remains explicit multiway control in shared IR.
No corpus result here promises GPU reconvergence, barrier participation,
derivative legality or subgroup behavior.

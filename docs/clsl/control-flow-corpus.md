# Bounded control-flow corpus

This test-only corpus exercises the existing compiler, not an alternative
structurizer. It extends the scalar regressions with typed raw value CFGs,
computed control facts, checked regions, target ASTs and emitted-source
execution models. Ordinary C# fixtures separately exercise actual Debug/Release
CIL and the public `CLSLCompiler.Parse/Compile/Emit` boundary.

Implementation: `DualDrill.CLSL.Test/ControlFlowCorpus{,Tests}.cs`.
The only existing helper extension is a typed raw-graph entry point in
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
| Baseline switch boundary | `DenseSwitchHasSwitchCilAndRetainsPublicFrontendRejection` | Dense ordinary C# switch really contains CIL `switch` in both builds; frontend rejection, not a structurizer failure |
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

## What is compared

For generated graphs a small host calculation over the input adjacency
encoding supplies a golden sum of visited node contributions and a node trace.
The typed raw CFG runs **before** facts and region construction. Subsequent
flat-region, scoped-continuation, normalized-region and emitted-source-model
executions must match results and traces. Hand cases additionally have fixed
numeric goldens and topology assertions.

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
  --filter 'FullyQualifiedName~ControlFlowCorpusTests|FullyQualifiedName~ScalarControlFlowTests'
nix develop --builders '' --command dotnet test \
  DualDrill.CLSL.Test/DualDrill.CLSL.Test.csproj --no-restore -c Release \
  --filter 'FullyQualifiedName~ControlFlowCorpusTests|FullyQualifiedName~ScalarControlFlowTests'
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

## Counterexample and capability ledger

| Observation | Classification | Status / next boundary |
|---|---|---|
| No unexpected wrong value/trace or reducible rejection in the specified bounded runs | Correct accepted scalar-model behavior | Finite evidence only; no general completeness claim |
| Two-entry / side-entry rejection | Intended out-of-contract rejection | No node-splitting repair implemented |
| Dense switch CIL rejected before structurization | Unsupported frontend | #155 / #158 owns explicit shared-IR multiway control; replace rejection expectation and rerun only after its accepted API is integrated |
| Reachable `NoExitPath` rejected for WGSL | Target capability restriction | IR/Slang emission remains a separate observation |
| Nonreturning model inputs exhaust budget | Oracle budget / unresolved execution | Neither successful equivalence nor a divergence theorem |

No genuine compiler counterexample required shrinking in these runs. Earlier
personal-review findings corrected **the tests and evidence** (empty facts
captures, an outside-loop branch mislabelled as two exits, disconnected C#
goldens); those were not production compiler fixes.

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

Switch remains explicit multiway control in the shared-IR workstream.
No corpus result here promises GPU reconvergence, barrier participation,
derivative legality or subgroup behavior.

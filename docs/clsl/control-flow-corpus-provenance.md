# Control corpus provenance

All new fixtures are independently authored; no upstream suite or source body
is copied or vendored. These sources inform topologies and semantic properties,
not assertions that their complete languages/backends are supported here.
Links pin full upstream revisions. The mapped corpus names are capture IDs.

| Source and exact paths | Revision / license | Property and local witness |
|---|---|---|
| WABT [brif-loop.txt](https://github.com/WebAssembly/wabt/blob/c1499564506fbd9b15e785f56d16e651b54cc7c2/test/interp/brif-loop.txt), [generate-label-names.txt](https://github.com/WebAssembly/wabt/blob/c1499564506fbd9b15e785f56d16e651b54cc7c2/test/roundtrip/generate-label-names.txt) | `c1499564506fbd9b15e785f56d16e651b54cc7c2`; [Apache-2.0](https://github.com/WebAssembly/wabt/blob/c1499564506fbd9b15e785f56d16e651b54cc7c2/LICENSE) | Counted loops; `hand-depth-*`, `metamorphic/sample-17*` |
| WebAssembly spec [loop.wast](https://github.com/WebAssembly/spec/blob/779957d81feca2ec6a372c40a9130e28ef390645/test/core/loop.wast), [br_if.wast](https://github.com/WebAssembly/spec/blob/779957d81feca2ec6a372c40a9130e28ef390645/test/core/br_if.wast) | `779957d81feca2ec6a372c40a9130e28ef390645`; [test/ Apache-2.0](https://github.com/WebAssembly/spec/blob/779957d81feca2ec6a372c40a9130e28ef390645/LICENSE) | Inner break/continue, selected values, effects after exit; `hand-same-target`, `hand-multiple-latches-exits`, reused scalar C# fixtures |
| Binaryen [relooper-merge6.c](https://github.com/WebAssembly/binaryen/blob/da9a372c83de9546c181e10cee0bf5588c532f76/test/example/relooper-merge6.c), [relooper-merge7.c](https://github.com/WebAssembly/binaryen/blob/da9a372c83de9546c181e10cee0bf5588c532f76/test/example/relooper-merge7.c), [flatten_rereloop.wast](https://github.com/WebAssembly/binaryen/blob/da9a372c83de9546c181e10cee0bf5588c532f76/test/lit/passes/flatten_rereloop.wast) | `da9a372c83de9546c181e10cee0bf5588c532f76`; [Apache-2.0](https://github.com/WebAssembly/binaryen/blob/da9a372c83de9546c181e10cee0bf5588c532f76/LICENSE) | Per-edge phi actions, loop side exit/shared tail, nested exit reconstruction; `hand-same-target`, `hand-multiple-latches-exits`, `hand-depth-*` |
| Slang [multiple-continue-sites.slang](https://github.com/shader-slang/slang/blob/282587ac1c04ad8cbd6592e6e12142a35b2687e8/tests/compute/multiple-continue-sites.slang), [multi-level-break.slang](https://github.com/shader-slang/slang/blob/282587ac1c04ad8cbd6592e6e12142a35b2687e8/tests/language-feature/multi-level-break.slang), [logic-short-circuit-evaluation.slang](https://github.com/shader-slang/slang/blob/282587ac1c04ad8cbd6592e6e12142a35b2687e8/tests/compute/logic-short-circuit-evaluation.slang), [infinite-loop.slang](https://github.com/shader-slang/slang/blob/282587ac1c04ad8cbd6592e6e12142a35b2687e8/tests/spirv/infinite-loop.slang) | `282587ac1c04ad8cbd6592e6e12142a35b2687e8`; [Apache-2.0 WITH LLVM-exception](https://github.com/shader-slang/slang/blob/282587ac1c04ad8cbd6592e6e12142a35b2687e8/LICENSE) | Multiple latches, nested transfers, effect multiplicity, no-exit distinction; `hand-multiple-latches-exits`, reused nested scalar controls, `divergence/*` |
| ILGPU [BasicLoops.cs](https://github.com/m4rs-mt/ILGPU/blob/ea51bcbdc3695554b8b9899a225d37c12cd0babe/Src/ILGPU.Tests/BasicLoops.cs), [BasicPhis.cs](https://github.com/m4rs-mt/ILGPU/blob/ea51bcbdc3695554b8b9899a225d37c12cd0babe/Src/ILGPU.Tests/BasicPhis.cs) | `ea51bcbdc3695554b8b9899a225d37c12cd0babe`; [University of Illinois/NCSA](https://github.com/m4rs-mt/ILGPU/blob/ea51bcbdc3695554b8b9899a225d37c12cd0babe/LICENSE.txt) | `LoopViewSwap` simultaneous values, value-changing continue paths, branch-selected loop joins; `hand-parallel-swap`, reused scalar C# controls |
| Cranelift [issue-13365.clif](https://github.com/bytecodealliance/wasmtime/blob/7ad2e732ab9ca8665d3cdd91f9c395315eeafc81/cranelift/filetests/filetests/egraph/issue-13365.clif) | `7ad2e732ab9ca8665d3cdd91f9c395315eeafc81`; [Apache-2.0 WITH LLVM-exception](https://github.com/bytecodealliance/wasmtime/blob/7ad2e732ab9ca8665d3cdd91f9c395315eeafc81/LICENSE) | Canonical `entry -> a/b; a -> b; b -> a`; `reject-two-entry-cycle` |

## Official Beyond Relooper artifact

Paper: [Beyond Relooper](https://doi.org/10.1145/3547621).
Official artifact v1.1: [Zenodo 6727752](https://doi.org/10.5281/zenodo.6727752),
`wtx.tar.gz`, embedded commit
`e915b07ee32d8f221faa1e9751444ed53f8ee2dd`.

Relevant paths are `wtx/src/Monomorphic.hs`, `wtx/src/Real.hs` (a symlink to
the GHC implementation), `inputs/fig1b.cmm` (diamond), `inputs/ex10.cmm`
(crossing/staggered joins), `inputs/irr.cmm` (two-entry cycle), and
`inputs/irrbad.cmm` (double cycle).
Local analogues are `generated/forward-4-0118`, `forward-4-0139` and
`reject-two-entry-cycle`, not literal translations of the full upstream cases.

Zenodo metadata declares CC-BY-4.0, but the archive contains no license file and
includes GHC-derived sources. Only topology ideas are used; no bundled code is
adapted. The unrelated 2026 `withlang-dev/beyond-relooper` repository is not the
official artifact and is not used.

The paper's central translator handles reducible CFGs; a separate node-splitting
pass repairs irreducible inputs. Its preliminary evaluation used roughly 30
Haskell/Cmm programs and differential action-trace interpreters, alongside an
inductive semantic argument. Passing this finite scalar corpus is not a
substitute for that general argument.

## Multiway ideas reserved for the accepted switch API

WABT [brtable.txt](https://github.com/WebAssembly/wabt/blob/c1499564506fbd9b15e785f56d16e651b54cc7c2/test/interp/brtable.txt)
and [br-table-loop.txt](https://github.com/WebAssembly/wabt/blob/c1499564506fbd9b15e785f56d16e651b54cc7c2/test/typecheck/br-table-loop.txt)
combine defaults, loop-header targets and enclosing exits.
Slang [nested-switch-continue-in-loop.slang](https://github.com/shader-slang/slang/blob/282587ac1c04ad8cbd6592e6e12142a35b2687e8/tests/bugs/nested-switch-continue-in-loop.slang)
separates switch exits from a containing-loop continue.
These are follow-up inputs for #155 / #158, not accepted multiway coverage in
the baseline corpus. Their licenses are the same project licenses above.

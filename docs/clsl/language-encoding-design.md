# Language, IR, and primitive encoding design

Status: **open design discussion**, started 2026-10-01.
Tracking: [#246](https://github.com/Hong-Xiang/drilla/issues/246).
Initial documentation: [#247](https://github.com/Hong-Xiang/drilla/pull/247).

This is the living record for choosing concrete C# encodings one requirement at
a time. It does not approve a new IR, implement new language support, or replace
the [current IR contract](ir_spec.md) and [pass contracts](compiler/passes.md).
Update this document as decisions are made; do not create a competing design
catalog. Earlier exploration in [functional_ir.md](functional_ir.md) and
[type_system.md](type_system.md) is historical evidence, not current capability.

Repository observations below use commit
`30a74f84fd0fd9abf0271d7756521b2d8827aab6`. External specifications are references,
not an expansion of the repository's supported profiles.

## 1. Requirements, candidates, and non-goals

### User requirements and clarified intent

- Cover CIL and WASM source/target concerns and Slang or SPIR-V-like shader
  targets. Inventory their differences before designing shared encodings.
- Keep dependencies directed from Compiler to Language. Language definitions
  must not acquire concrete compiler algorithms merely to share implementation.
- Preserve the useful identity/interning role of generic singleton witnesses,
  or replace it explicitly. User structures are not a closed builtin set.
- Separate object-language types from CLR types. Array lengths, user declarations,
  and metadata need not be encoded exhaustively as CLR generic arguments.
- Support both reuse axes: one operation over different types/shapes, and
  common handling of different operations over one type/shape.
- Discuss implementable, typed C# signatures, construction, consumption, and
  metadata binding, not only conceptual diagrams.
- Treat `FunctionalExperiment` as an independent lightweight-HKT/object-algebra
  experiment. Its mechanisms are candidates, not an adopted compiler design.

### Candidates, not decisions

Term-level type descriptions, small multi-sorted generalized object algebras,
optional recursive carriers, `DiMap`, generic witnesses at authoring boundaries,
and context-based interning are under discussion. No requirement below silently
selects one of these mechanisms. In particular, neither "all `.Instance` access
is forbidden" nor "all attributes become enums" has been decided.

This issue does not authorize a universal HKT framework, a compiler-wide rewrite,
full CIL/WASM support, a new SPIR-V backend, or a dependency on ILGPU. Generated
math sources remain generator-owned. API changes and migrations require their
own reviewed slices.

## 2. Language inventory

"Language" includes a type-description signature and a value/effect/control
signature; it does not imply that either is represented by CLR type parameters.
Several rows below are stages or projections, not separate external languages.

### Current boundaries and roles

| Surface | Role and current repository evidence | Boundary to preserve |
|---|---|---|
| CLR authoring and CIL | Implemented restricted input via reflection and original bytecode, followed by explicit stack and value passes [R1]. CIL output remains a design concern, not a current public output target. | CLR metadata identity and original opcode/offset information survive collection; binding and stack analysis are separate steps. |
| WASM Core | Input is a proposed bounded `i32` profile with reference fixtures, not an implemented frontend [R2]. Output is a bounded shared-IR-to-WAT prototype, not a full public compiler target [R3]. | Native stack/control validation and capability admission precede conversion; WAT is not itself emitted binary bytecode. |
| Shared math/value IR | Implemented typed operations, operands/results, block arguments, and scoped regions, with documented gaps [R4]. | Logical types, effect order, value identity, control references, and containment are distinct. |
| Slang-facing target | Implemented `SlangTargetLowering` produces a target AST; `SlangEmitter` prints it; `SlangService` invokes Slang for WGSL [R5]. This is not a full Slang parser or a model of Slang's internal IR. | Target control placement/legalization belongs before syntax emission. |
| WGSL | Existing downstream public output through Slang [R5]. | WGSL capabilities, shader execution requirements, and layout are not inferred from CPU math storage. |
| SPIR-V | An experimental emitter exists but SPIR-V is not in the public target enum [R5, R6]. A SPIR-V-like internal representation and a conforming SPIR-V module are different goals. | Types, IDs, capabilities, decorations, and client execution-environment rules must be modeled explicitly before claiming support. |

### Information each encoding must retain

The external-language requirements are grounded in ECMA-335, the WASM Core
specification, Slang documentation, and the SPIR-V specification [S1-S4].
These are design requirements to examine, not claims of full implementation.

| Surface | Types and identity | Terms/control | Metadata, effects, and unresolved questions |
|---|---|---|---|
| CIL | Source nominal types, generic instantiations, declared types versus evaluation-stack categories, managed references. | Opcode plus typed immediate; implicit stack; branches, calls, and exception regions. | Metadata tokens and source offsets; checked arithmetic, exceptions, aliasing. Decide raw versus bound instruction ownership and preserve unsupported cases for explicit rejection. |
| WASM | Module type/index spaces; stack signatures; native types rather than shader aliases. | Structured blocks/loops and relative label depth; operand stack and indexed locals. | Traps, memory/table effects, feature profile. Full Core has more than the initial `i32` subset, including multi-value signatures; do not design around a universal single result. |
| Shared IR | Canonical semantic type references and nominal declarations; stable SSA and label identities. | Precise primitive application, ordered effects, block parameters and explicit transfers. | Provenance and completed analysis facts remain separate from operation semantics. Decide which invariants are local and which require graph validation. |
| Slang-facing | Mapped value/resource types and declarations. | Target expressions, statements, calls, and structured control. | Stage/interface annotations and target spelling. Do not assume every source operation has a direct builtin with the same name. |
| SPIR-V | Explicit type/result IDs, storage classes, module declarations. | Typed instruction families, function blocks, merge/control instructions and references. | Decorations, memory model, capabilities and environment-specific legality. Numeric opcode similarity alone is not a portability contract. |

First inventory examples: scalar arithmetic; comparison with a distinct result
sort; vector broadcast/reduction; a fixed array; a user struct with annotated
members; a branch with different argument tuples to the same target; and a loop.
Record both positive and unsupported cases for each applicable language.

### Ownership proposal to evaluate

```text
Language definitions
  type/operation signatures, identity rules, syntax, local construction contracts
  optional algebra/visitor and mapping protocols

Authoring/projection boundary
  CLR types and attributes <-> explicit language definitions

Compiler
  source binding/validation, analyses, lowering, target realization
  interpreter implementations and pass-local state
```

Possible `Language.CIL` and `Language.WASM` packages are organizational candidates,
not a requirement for one universal generic node. Current Language also contains
transforms and depends on Graphics; package cleanup needs an explicit migration,
not a claim that the desired separation already holds [R7].

## 3. Algebra, coalgebra, representation, and recursion

Use precise types when discussing the dual views. For a one-layer signature `F`
with a lawful map, `F A -> B` is a generalized algebra. The dual-shaped
`A -> F B` is a generalized producer. Their input/output carriers do not have to
match, but the usual recursive operators need the following specializations:

```text
algebra:     alpha : F B -> B
coalgebra:   beta  : A -> F A

cata alpha : Fix F -> B
ana beta   : A -> Fix F
hylo alpha beta : A -> B

cata alpha    = alpha . fmap(cata alpha) . out
ana beta      = in . fmap(ana beta) . beta
hylo alpha beta = alpha . fmap(hylo alpha beta) . beta
```

Here `in`/`out` wrap/unwrap one layer. This finite, well-founded presentation
does not promise termination for arbitrary producers or cyclic program graphs.
Infinite/productive unfoldings need a separately chosen lazy/coinductive model.
The recursion-scheme reference is [S5]; the syntax fixed point is not a compiler
worklist reaching a stable dataflow solution.

A source-to-target translation need not use the same signature twice:
`alpha : F (Fix G) -> Fix G` can fold source syntax into target syntax.
That does not establish semantic preservation or imply a reverse translation.
Do not fuse away an intermediate representation solely from the word "hylo":
diagnostics, source identity, sharing, failures, and observable effect order may
require materialized boundaries.

### Candidate C# shape

The following is illustrative notation for discussion, not a compiled prototype
or proposed production API. `Arithmetic` is intentionally only a tiny example.

```csharp
interface IArithmetic<in TI, out TO>
{
    TO Literal(int value);
    TO Add(TI left, TI right);
}

interface IArithmeticLayer<T>
{
    R Evaluate<R>(IArithmetic<T, R> algebra);
    IArithmeticLayer<R> Map<R>(Func<T, R> map);
}
```

`TI` can be child terms, computed values, or SSA references; `TO` can be a node,
a value, a document, or a builder result. A factory algebra can construct a tree
or intern a node without requiring those two representations to be identical.
An optional recursive wrapper closes the child slot only when appropriate.

For pure carrier adapters:

```text
alg : Alg<I,O>, f : I' -> I, g : O -> O'
DiMap(f,g,alg) : Alg<I',O'>

adapted.Add(x,y) = g(alg.Add(f(x),f(y)))
adapted.Literal(n) = g(alg.Literal(n))

DiMap(id,id,alg) = alg
DiMap(h,k,DiMap(f,g,alg)) = DiMap(f . h,k . g,alg)
Map(f,node).Evaluate(alg) = node.Evaluate(DiMap(f,id,alg))
```

These laws concern the same language signature and pure maps. A language
translation, scalar-to-vector lifting, error sequencing, or a stateful compiler
pass is not automatically a `DiMap`. Keep the name `DiMap` in the discussion;
`CoSelect` has not been selected as a second API.

### Composition and storage questions

- Combine coherent fragments, not one interface per overload. An interpreter
  for pure numerics must not implement barriers just to satisfy a giant visitor.
- Keep type, value, label, declaration and any truly distinct value sorts
  separate. Multi-sorted algebra composition needs explicit carrier mappings.
  Interface inheritance combines contracts, not implementations by itself.
- Nested generic dispatch is useful only when recovered type parameters enable
  shared constrained behavior. It cannot remove genuinely different pairwise
  rules, prove arbitrary shape equality, or justify unchecked casts.
- For trees, compare fold/unfold and direct algebra construction. For interned
  DAGs, preserve sharing and define memoization. For nominal types and CFGs, keep
  explicit references/binders and a graph traversal or solver.
- Specify strictness. Folding both children before interpreting a conditional
  may execute the unselected branch. Delayed/environment carriers or explicit
  control are choices to evaluate, not implicit purity guarantees.
- Type interning, definition binding and analysis convergence have different
  scopes. Do not use mutable member lists as dictionary keys or confuse reference
  equality across contexts with portable identity.

## 4. Existing abstractions to learn from

| Existing surface | Useful mechanism | Gap to address, not silently adopt |
|---|---|---|
| `Types/VecType.cs`, `Types/INumericType.cs`, `Operation/OpKind.cs` [R8] | Singleton witnesses, operation/type parameters, and visitor bridges from heterogeneous terms back to generics. | A generic witness need not be the runtime type representation; arbitrary user declarations cannot honestly implement a unique `Instance`. |
| `ShaderFunction.cs`, `ShaderOperator.cs`, `Operation/BinaryExpressionOperation.cs` [R8] | Overload expansion and default signature construction. | Separate authorities; `distance`/`refract` demonstrate signature drift. Constraints and attributes do not prove CLR signature agreement. |
| `Terminator.cs`, `Seq.cs`, `Region/RegionTree.cs` [R4] | Multi-carrier terms, factories, maps, optional recursion, strict/lazy folds. | Containment traversal is not execution or control-reference traversal. Reuse valid mechanisms without rewriting CFG semantics. |
| `FunctionalExperiment/RecursiveScheme/IntLang.cs` [R9] | `ProSelectAlgebra` demonstrates input/output adaptation and default implementations. | Prototype status; do not infer a production semantic contract from it. |
| `FunctionalExperiment/RecursiveScheme/ArithExpr.cs` [R9] | Integer/bool fragment composition and multiple interpreters. | Composition plumbing and carrier relationships must be shown in concrete C#. |
| `FunctionalExperiment/RecursiveScheme/Common.cs` and `Kind/Kind1.cs` [R9] | Factory-derived maps, static and singleton witnesses, optional fixed points. | Independent HKT research; the associated-algebra projection in `IntLang.cs:15-16` uses a cast. Do not import a universal encoding until it beats narrow typed interfaces. |

For primitive generation, preserve both axes of reuse but do not derive behavior
from arity alone. A component operation, reduction, matrix product, and
context-dependent intrinsic can share some machinery without sharing semantics.
The active generator remains `DualDrill.Mathematics.CodeGen` ->
`DualDrill.APIDefinition/DMath` -> `DualDrill.Mathematics/*.gen.cs` [R10].

## 5. ILGPU: pinned comparison, not a chosen architecture

Inspected the user-provided checkout at
`ea51bcbdc3695554b8b9899a225d37c12cd0babe` (commit dated 2026-04-16).
No claim is made about current upstream. No implementation or dependency is
imported. Its [license file][I0] is University of Illinois/NCSA.

| Observation at that revision | Possible lesson and limitation |
|---|---|
| CIL disassembly, method declaration, CFG/SSA generation, completion and verification are staged [I1]. | Useful input-boundary comparison; not a reason to embed compiler state into language nodes. |
| A managed-type/address-space cache [I2] coexists with [structural type unification][I2b] and [layout-based structure equality][I2c]. | Distinguish source nominal identity from lowered representation identity. Matching field layouts can canonicalize together; that is not the equivalence rule we must choose for source structs. |
| Concrete value classes expose kind, visitor dispatch and rebuilding; arithmetic is grouped into operation families [I3]. | A closed OO reference implementation, not evidence that ILGPU uses generalized object algebras. Compare its repetition against candidate algebras. |
| Graph rebuilding maps blocks/values and completes phis [I4]; [analysis uses a worklist][I4b]. | Relevant counterexample to treating cyclic SSA as an ordinary expression tree. |
| Generic backend setup [specializes intrinsics][I5a]; the PTX path checks implementation availability before emission [I5], using an explicit [mapping check][I5b]. | Useful separation, not a claim about every backend's policy. Its [backend taxonomy][I5c] is IL/Velocity/PTX/OpenCL, not WASM/Slang/SPIR-V evidence. |
| Intrinsic matching uses method identity [I6]; frontend dispatch has [attribute handlers][I6a]; code-generation mappings [create delegates][I6b]. | Study explicit binding points, but do not claim these provide compile-time agreement between an attributed CLR signature and mathematical semantics. |
| Generated intrinsic matcher families use T4 [I7]. | Generate proven repetitive structure; this does not select T4 or make the generated catalog an independent semantic oracle. |

The struct-equivalence conclusion is an inference from the [interning][I2b] and
[`StructureType.Equals`][I2c] implementations, not a claim that ILGPU models every
source-language distinction structurally.

## 6. Ordered discussion and experiment ledger

No encoding decision is approved by this initial document. Each row is a small
design question; implementations will be split further where independently
shippable. Do not open implementation PRs for the whole table automatically.

| ID | Status | Question and required concrete evidence |
|---|---|---|
| D01 | Open; discuss first | Choose the language/stage boundaries and smallest first signature. Show CIL raw/bound terms, a WASM stack instruction, shared operation, and target term for the same bounded example; identify all non-shared semantics. |
| D02 | Open | Type construction and identity: compare pure terms, witnesses, and context/global interning. Show scalar/vector/matrix, fixed array length, two nominal structs with identical fields, member metadata, and cross-context behavior. |
| D03 | Open | Algebra composition and `DiMap`: show compileable narrow C# interfaces, a term factory, printer and evaluator, fragment composition, and law checks. Compare against direct term matching and existing visitor plumbing; count casts and adapters, not only interface lines. |
| D04 | Open | Primitive family/signature binding: preserve shared type variables and both reuse axes. Show add, comparison, broadcast, dot, distance, refract, conversion and matrix multiplication; include incompatible shapes and unsupported scalar families. |
| D05 | Open | Authoring identity: compare enum ID, family marker and closed witness attributes. Show generated and handwritten call sites, exact CLR signature mismatch diagnostics, and where validation runs. |
| D06 | Open | Representation/recursion: contrast one expression tree, an interned DAG, nominal type references, a shared join and a loop. Specify strictness, identity, traversal and error behavior; ordinary cata is not the CFG solver. |
| D07 | Open | Language adapters and target capability: show CIL checked versus wrapping arithmetic, WASM trap/control behavior, and shader-only effects without equating names or broad families. |
| D08 | Open | Migration/generation: select one approved vertical slice, migrate every affected caller, regenerate deterministically including stale-file detection, and preserve compiler/CPU/target evidence. |

For D01, a proposed starting comparison is wrapping `i32` addition plus a
conditional with a shared successor. It exercises numeric reuse and control
identity without presupposing full matrix or resource support. This is a
discussion suggestion, not the selected implementation scope.

### Record format for each decision

When resolving a row, append its date and exact approval reference here, with:

1. Requirement, source/target profile, inputs, outputs, and invariants.
2. Concrete C# definitions and construction/consumption examples.
3. Static guarantees versus named runtime checks.
4. Alternatives, including the smallest direct term implementation.
5. Examples, counterexamples, algebraic laws where applicable, and independent
   CPU/compiler/target evidence where semantics depend on them.
6. Decision, rejected alternatives, remaining limitations, migration plan, and
   implementation issue/PR references.

Until then, illustrative code is not a validated prototype. Later experiments
must use existing build/test infrastructure and record actual outputs; no new
toolchain or generator is implied by this document.

## 7. Related work and scope coordination

[#243](https://github.com/Hong-Xiang/drilla/issues/243) owns executable math and
intrinsic-generation outcomes; [#244](https://github.com/Hong-Xiang/drilla/issues/244)
owns reflection/ABI separation. This design track supplies representation and
ownership decisions, not a replacement backlog.

Coordinate CIL staging with #114; target ASTs with #115; WASM output/input with
#116/#118; SSA/control/regions with #111/#113/#117; resources/cooperation with
#168/#167; math definitions/SIMD with #67/#74. Existing profile restrictions and
generated-source ownership remain in force until explicitly migrated.

## References

Repository references are relative to this document; line numbers refer to the
snapshot above. Existing contract documents should be read before historical
architecture sketches.

- **R1:** [CIL stages](compiler/linear-cil.md), [passes](compiler/passes.md),
  [native CIL dispatch](../../DualDrill.ILSL/Frontend/CilInstructionInfo.cs).
- **R2:** [WASM input profile](../../specs/wasm/scalar-i32-profile.md).
- **R3:** [WASM output contract](backends/wasm.md),
  [target plan](../../DualDrill.ILSL/Backend/Wasm/WasmPlan.cs).
- **R4:** [IR contract](ir_spec.md),
  [instruction](../../DualDrill.CLSL.Language/Instruction/IInstruction.cs),
  [terminator](../../DualDrill.CLSL.Language/Terminator.cs),
  [sequence](../../DualDrill.CLSL.Language/Seq.cs),
  [regions](../../DualDrill.CLSL.Language/Region/RegionTree.cs).
- **R5:** [public compiler](../../DualDrill.ILSL/CLSLCompiler.cs), lines 24-29,
  84-142; [Slang AST](../../DualDrill.ILSL/Backend/SlangTargetAst.cs).
- **R6:** [experimental SPIR-V emitter](../../DualDrill.ILSL/Backend/SPIRVEmitter.cs).
- **R7:** [Language project](../../DualDrill.CLSL.Language/DualDrill.CLSL.Language.csproj),
  [existing transform](../../DualDrill.CLSL.Language/Transform/FunctionToOperationPass.cs).
- **R8:** [types](../../DualDrill.CLSL.Language/Types/Types.cs),
  [vector types](../../DualDrill.CLSL.Language/Types/VecType.cs),
  [numeric types](../../DualDrill.CLSL.Language/Types/INumericType.cs),
  [operation interfaces](../../DualDrill.CLSL.Language/Operation/OpKind.cs),
  [binary operation](../../DualDrill.CLSL.Language/Operation/BinaryExpressionOperation.cs),
  [functions](../../DualDrill.CLSL.Language/ShaderFunction.cs),
  [operators](../../DualDrill.CLSL.Language/ShaderOperator.cs).
- **R9:** [IntLang](../../FunctionalExperiment/RecursiveScheme/IntLang.cs), lines
  15-16, 24-70; [ArithExpr](../../FunctionalExperiment/RecursiveScheme/ArithExpr.cs),
  lines 5-10, 93-177; [Common](../../FunctionalExperiment/RecursiveScheme/Common.cs),
  lines 75-115; [Kind1](../../FunctionalExperiment/Kind/Kind1.cs), lines 40-62.
- **R10:** [generation entry](../../DualDrill.Mathematics.CodeGen/Program.cs),
  [generator policy and known drift](../../README.md#mathematics-generation).

Primary external references, consulted 2026-10-01. Moving specification pages
must be pinned to a chosen profile/version in any implementation proposal.

- **S1:** [ECMA-335, Common Language Infrastructure](https://ecma-international.org/publications-and-standards/standards/ecma-335/),
  especially metadata and CIL instruction semantics.
- **S2:** [WASM specifications](https://webassembly.github.io/spec/) and
  [Core instructions](https://webassembly.github.io/spec/core/syntax/instructions.html).
- **S3:** [Slang compilation targets](https://shader-slang.org/slang/user-guide/targets).
- **S4:** [SPIR-V specification](https://registry.khronos.org/SPIR-V/specs/unified1/SPIRV.html),
  including execution-environment requirements.
- **S5:** Meijer, Fokkinga, Paterson,
  [Functional Programming with Bananas, Lenses, Envelopes and Barbed Wire](https://maartenfokkinga.github.io/utwente/mmf91m.pdf).
- **S6:** Oliveira, Cook,
  [Extensibility for the Masses](https://www.cs.utexas.edu/~wcook/projects/oa/oa.pdf).
  Object-algebra reference; our `DiMap` candidates also have direct local
  experimental evidence [R9].

ILGPU references are immutable permalinks at the inspected revision:

[I0]: https://github.com/m4rs-mt/ILGPU/blob/ea51bcbdc3695554b8b9899a225d37c12cd0babe/LICENSE.txt
[I1]: https://github.com/m4rs-mt/ILGPU/blob/ea51bcbdc3695554b8b9899a225d37c12cd0babe/Src/ILGPU/Frontend/ILFrontend.cs#L445-L487
[I2]: https://github.com/m4rs-mt/ILGPU/blob/ea51bcbdc3695554b8b9899a225d37c12cd0babe/Src/ILGPU/IR/Types/IRTypeContext.cs#L302-L342
[I2b]: https://github.com/m4rs-mt/ILGPU/blob/ea51bcbdc3695554b8b9899a225d37c12cd0babe/Src/ILGPU/IR/Types/IRTypeContext.cs#L505-L518
[I2c]: https://github.com/m4rs-mt/ILGPU/blob/ea51bcbdc3695554b8b9899a225d37c12cd0babe/Src/ILGPU/IR/Types/StructureType.cs#L983-L1005
[I3]: https://github.com/m4rs-mt/ILGPU/blob/ea51bcbdc3695554b8b9899a225d37c12cd0babe/Src/ILGPU/IR/Values/Arithmetic.cs#L115-L200
[I4]: https://github.com/m4rs-mt/ILGPU/blob/ea51bcbdc3695554b8b9899a225d37c12cd0babe/Src/ILGPU/IR/Construction/IRRebuilder.cs#L420-L464
[I4b]: https://github.com/m4rs-mt/ILGPU/blob/ea51bcbdc3695554b8b9899a225d37c12cd0babe/Src/ILGPU/IR/Analyses/FixPointAnalysis.cs#L205-L233
[I5]: https://github.com/m4rs-mt/ILGPU/blob/ea51bcbdc3695554b8b9899a225d37c12cd0babe/Src/ILGPU/Backends/PTX/PTXBackend.cs#L195-L207
[I5a]: https://github.com/m4rs-mt/ILGPU/blob/ea51bcbdc3695554b8b9899a225d37c12cd0babe/Src/ILGPU/Backends/Backend.cs#L837-L861
[I5b]: https://github.com/m4rs-mt/ILGPU/blob/ea51bcbdc3695554b8b9899a225d37c12cd0babe/Src/ILGPU/Backends/Backend.cs#L343-L363
[I5c]: https://github.com/m4rs-mt/ILGPU/blob/ea51bcbdc3695554b8b9899a225d37c12cd0babe/Src/ILGPU/Backends/Backend.cs#L59-L83
[I6]: https://github.com/m4rs-mt/ILGPU/blob/ea51bcbdc3695554b8b9899a225d37c12cd0babe/Src/ILGPU/IR/Intrinsics/IntrinsicMatcher.cs#L130-L173
[I6a]: https://github.com/m4rs-mt/ILGPU/blob/ea51bcbdc3695554b8b9899a225d37c12cd0babe/Src/ILGPU/Frontend/Intrinsic/Intrinsics.cs#L79-L155
[I6b]: https://github.com/m4rs-mt/ILGPU/blob/ea51bcbdc3695554b8b9899a225d37c12cd0babe/Src/ILGPU/IR/Intrinsics/IntrinsicMapping.cs#L306-L324
[I7]: https://github.com/m4rs-mt/ILGPU/blob/ea51bcbdc3695554b8b9899a225d37c12cd0babe/Src/ILGPU/IR/Intrinsics/IntrinsicMatchers.tt#L18-L66

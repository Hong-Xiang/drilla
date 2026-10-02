# Language, IR, and primitive encoding design

Status: **concrete v1 proposal for discussion**, revised 2026-10-02.
Tracking: [#246](https://github.com/Hong-Xiang/drilla/issues/246).
Initial documentation: [#247](https://github.com/Hong-Xiang/drilla/pull/247).

This is the living record for choosing concrete C# encodings one requirement at
a time. It does not approve a new IR, implement new language support, or replace
the [current IR contract](ir_spec.md) and [pass contracts](compiler/passes.md).
The first revision was an inventory, not a sufficient design. Sections 4-5 now
specify carriers, algebra methods, binding rules, instruction ownership, and
non-toy modeling obligations. The accompanying
[executable contract specimen](encoding/EncodingContractProbe.cs) is isolated
from every production project. It demonstrates selected contracts, not a new
compiler or numerical implementation.

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

The concrete v1 candidate uses context-canonical term descriptions, nominal
declaration identities, small multi-sorted algebras, and checked interpretation
into ordered IR. These are specific proposals to accept, revise, or reject,
not approved production changes. Generic authoring witnesses and global
primitive singletons remain alternatives at the boundary; neither banning
all `.Instance` access nor replacing every attribute with an enum is decided.

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

### Carrier mapping is not the design acceptance example

The equations below state the generic law only. The concrete acceptance surface
is types, vector geometry, nominal declarations, address/resource operations,
result-bearing effects, and mixed edge tuples in sections 4-5. A successful
integer-add example alone does not establish that the design fits this compiler.

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

## 4. Concrete v1 encoding

This section makes choices rather than listing interchangeable ideas. The
specimen implements the indicated subset. Signatures explicitly marked **draft**
are precise design contracts but are not implementations in the specimen.
None is a production API commitment.

### 4.1 Carrier sorts and canonical identity

Do not use one carrier for mathematical data, core addresses, resource bindings,
target assignable expressions, and declarations.

| Sort | v1 representation and equality | Construction/admission rule |
|---|---|---|
| Semantic type | Opaque `TypeTerm` reference, owned by `TypeContext`; one immutable description per handle. | Structural factories intern within the context; a null/foreign/noncanonical handle is rejected. A type from another context must be imported, not compared by display name. |
| Nominal type declaration | Fresh declaration identity with ordered immutable members and declaration/member metadata. In the specimen the nominal `TypeTerm` is also the declaration token. | `DefineStruct` always declares a new type. The source adapter maps the same CLR/module declaration to its existing token instead of repeatedly calling `DefineStruct`. |
| Runtime data | `DataRef`, owned by a function builder and carrying a data type. | Not a resource, address, function, or label; matching type does not imply matching value identity or owner. |
| Core address/byref | `AddressRef` plus pointee, address space and access. | Can participate in explicitly address-typed slots. Address stability/escape/dominance is not proved by minting the reference. |
| Resource binding | Distinct `BufferRef`, `TextureRef`, `SamplerRef`, owned by a module/resource arena. | A wrapper's CLR value-type status does not make it ordinary shader data. The current profile rejects these in arrays, user structs, ordinary locals and function parameters. |
| Control/declaration identity | Function, member and label tokens; display names are not keys. The specimen's `MemberSymbol` is canonical by `(nominal owner, ordinal)`. | A member belongs to exactly one declaring struct; a label belongs to a function. Branch arms retain their own identity even when targets match. |
| Slang place | A target syntax tree rooted in a variable/parameter, with member/index/component selections. | Not the core byref carrier. A swizzle such as `zy` is not an ordinary pointer to a contiguous `vec2`. |

The proposed public type operations inspect **the same** immutable description
via pattern matching or algebra dispatch. These are observation APIs, not two
editable authorities. A single-threaded construction session freezes definitions
before publication; parallel compiler passes do not mutate the session.

`StructMember` is a field **definition**, not a field-reference token. To project
an address, obtain `MemberSymbol(owner, ordinal)` from the type context, then:

```text
Field(AddressRef(owner,space,access), MemberSymbol(owner,i))
  -> AddressRef(owner.members[i].type, space, access)
```

Both occurrences of `owner` must be the same nominal handle. An equal-shaped
different owner fails before a projected address is minted. This exact rule,
canonical member lookup, and `Field -> Load` are exercised in the specimen.

The specimen's structural keys use canonical child handles and value fields.
User structs bypass structural deduplication. Generated `frexp` result
declarations are cached by `(intrinsic, input type)` in the type context, not
in each binder. Two binders must produce the same result type.

For forward/recursive declarations the **draft extension** is
`Declare(sourceKey) -> StructSymbol`, then `Define(symbol, members)`, then
`Freeze()`. No incomplete definition is published. By-value cycles remain
invalid; pointer/reference cycles require explicit support. This is not
implemented by the specimen's atomic `DefineStruct`.

### 4.2 Type-description algebras

These interfaces are compiled in the specimen:

```csharp
public interface IDataTypes<in T, in N, out R>
{
    R Scalar(ScalarKind kind);
    R Vector(T element, int rank);
    R Matrix(T element, int rows, int columns);
    R FixedArray(T element, int length);
    R Nominal(N declaration);
}

public interface IResourceTypes<in T, out R>
{
    R Texture(T sample);
    R Sampler();
    R Buffer(T element, BufferAccess access);
}

public interface ITypes<in T, in N, out R> :
    IDataTypes<T, N, R>, IResourceTypes<T, R>
{
}
```

`T` means a referenced child type, not a recursively evaluated runtime value.
`N` means a nominal token, not its member list. `TypeContext.Evaluate` performs
one-layer dispatch; `CanonicalTypeFactory` reifies that layer into the same
context. It must preserve both structural canonicality and nominal identity.

Binding obligations are explicit:

| Constructor | Exact rule | Specimen/profile limitation |
|---|---|---|
| Scalar | A known scalar kind. Boolean is not numeric. | Bool/i32/u32/f32; production i/u8/16/64 and f16/f64 must be added deliberately, not silently coerced. |
| Vector | Scalar element, rank 2-4. | Numeric operations further constrain the element; constructing `vec<bool>` does not admit arithmetic on it. |
| Matrix | Float element, mathematical row and column counts 2-4. | f32 only in specimen. Storage order and ABI are absent from logical shape. |
| Fixed array | Data element, positive compile-time length. | Specimen uses positive `int`; full-u32 lengths require a checked length value and allocation/target limit policy, not a type-level bit encoding. |
| Nominal struct | Declared token plus frozen ordered member definitions. | Same names/layout do not make two declarations equal. Resource members are rejected in this profile. |
| Structured buffer | Data element restricted to f32/i32/u32, read-only or read-write access. | Resource binding, not array/value subtyping. |
| Texture/sampler | Distinct resource descriptions. | Only the current f32 sampled-2D and ordinary-sampler shape is modeled. |

Pointees, signatures, and metadata need additional sorts rather than pretending
they are scalar values. The **draft** nonrecursive records are:

```text
AddressType = (Pointee: DataType, Space: AddressSpace, Access: AccessMode)
SlotType    = Data(DataType) | Address(AddressType)
Parameter   = Value(DataType) | ByRef(AddressType, RefMode)
FunctionSig = (Parameters: ordered Parameter[], Results: ordered SlotType[])
```

`RefMode` distinguishes `in/ref/out` source obligations; it is not inferred from
read/write permission alone. A signature's empty result row means no result:
there is no SSA `Unit` value. A multi-result row is different from a one-element
row containing a product/struct. The current CLSL/CIL targets still admit at most
one supported result; representing a row does not change that profile.

Metadata belongs to named declarations/members/uses, not arbitrary type keys:
location/binding/stage remain interface data; member alignment remains a
declaration constraint; target offsets/strides remain ABI data. A qualifier that
changes type legality, such as address space, belongs in the type description.

### 4.3 Operation definitions, binding plans, and exact signatures

The chosen candidate has one operation discriminator: the bound descriptor.
Do not store a family ID and separately select a behavior-specific algebra
method that could contradict that ID.

The specimen compiles:

```csharp
public interface IPrimitive<in V, out R>
{
    R Apply(BoundOperation operation, IReadOnlyList<V> arguments);
}

public sealed record PrimitiveLayer<V>(
    BoundOperation Operation, ImmutableArray<V> Arguments)
{
    public R Evaluate<R>(IPrimitive<V, R> algebra) =>
        algebra.Apply(Operation, Arguments);
}
```

`BoundOperation` is a source binding plan with no public constructor or writable
properties. Its ordered source/canonical operand types and adaptations are
derived by the binder; its `ClosedPrimitive` owns family, a semantic-contract
identity, canonical parameters and result. Normalized `PureStep` stores **only** that closed primitive, not an
authoring plan that a later visitor could apply twice.

A raw `PrimitiveLayer` is still not checked IR: the builder
rechecks the actual operand owners, arity and source types before emitting any
step. `IReadOnlyList<V>` is intentional here: operations have data operands;
calls with data/byref arguments use a different sum and algebra.

This modest runtime operand row is the current trade-off: C# statically
distinguishes data from address/resource carriers, while relational signature
checks handle arbitrary runtime types. It avoids an interface per overload and
does not claim dependent typing through generic constraints.

The binding rules, not the method names, define the operation:

| Family | Exact binding relation | Normalization/result |
|---|---|---|
| Component add/multiply | Equal numeric scalar/vector/matrix shape. Scalar/vector overloads require the scalar to equal the vector element type. | Preserve operand order; insert a splat only on the scalar side. Matrix component multiply is distinct from matrix product. |
| Unary component math / comparison | **Draft:** choose an admissible scalar rule and lift over the same shape. A comparison maps element type to bool, rather than retaining the operand element. | No blanket arithmetic implementation for bool; vector comparison returns a bool vector. |
| Dot | Two identical numeric vector types. | Returns the element scalar, never a vector. Reduction order and integer/float semantics belong to the numerical contract. |
| Distance | Two identical float scalars or two identical float vectors. | Returns the float scalar; no scalar/vector mixing or implicit broadcast. |
| Refract | `Vec<N,F>, Vec<N,F>, F`. | Returns `Vec<N,F>`. Eta **stays scalar even in the canonical operation**; a backend builtin must never receive broadcast eta. |
| Ldexp | `F,i32` or `Vec<N,F>,Vec<N,i32>`. | Returns the first input type; a scalar exponent is not silently broadcast for the vector signature. |
| Matrix product | `Mat<R,K,F>,Mat<K,C,F>`. | `Mat<R,C,F>`. **Draft:** matrix-vector is `(R,K)*(K)->R`; vector-matrix is `(R)*(R,C)->C`, independent of memory layout. |
| Frexp | Float scalar or vector `T`; exponent type has the same shape and i32 elements. | One generated nominal result `{fraction:T, exponent:IntShape(T)}`. Not two SSA results. |
| Constructors / projection | **Draft:** vector component widths sum exactly to N; structure fields match declaration order and owner; swizzle indices are in range. | Zero/splat/composite are distinct binding rules; repeated read lanes are legal, repeated write lanes are not. |
| Convert / bitcast | **Draft:** distinct rules with exact source/target types. | Value conversion is not equal-width bit reinterpretation; language-specific overflow/NaN/domain rules remain explicit. |

For example the **executed specimen chain** is:

```text
incident, normal : vec3f32; eta : f32
  refract(incident,normal,eta) -> direction:vec3f32
  dot(direction,direction)    -> norm:f32
  splat(norm)                -> norm3:vec3f32
  componentMul(direction,norm3) -> scaled:vec3f32
```

The multiplication's binding plan retains source `(vec3f32,f32)` and canonical
`(vec3f32,vec3f32)` signatures. `IrBuilder.Apply` emits the splat explicitly and
checks all source operands first, so rejection leaves no partial instruction
sequence. Its resulting `PureStep` has canonical operand types, so subsequent
passes never see a vector operand paired with a scalar parameter. A core
interpreter consumes `ClosedPrimitive` and canonical data operands; it does not
rerun the source adapter. The operation family is authoritative; `Apply` does
not infer CPU behavior from the shape.

The operation key is `(family, semantic contract, canonical parameter types)`,
not just family and operand types. The specimen represents the contract identity
with `PrimitiveSemantics`; it is not a claim that the numerical interpreter has
been implemented. `BindSourceAdd` maps CIL unchecked-i32 and WASM-i32 add to the
explicit `ModularInteger32` contract and rejects checked CIL before binding.

The **numerical contract draft** distinguishes modular integer operations from
float component operations and family-specific reference operations. Modular
integer add/multiply reduce modulo 2^32, with interpretation determined by the
signed/unsigned type. Float component operations and reductions must specify
rounding, contraction, exceptional values and reduction order before a target
can claim compatibility. The specimen's `Float32Component`/`Float32Reference`
tokens reserve those distinct contracts but do not complete their numerical
specifications. They cannot be treated as a default permission for fast math.

CPU definitions, target implementation hooks and contract-conformance evidence
remain #243 obligations. The design now has an explicit place for that identity;
it does not execute `refract` or claim transcendental accuracy.

### 4.4 Data, addresses, resources, calls, and results

The proposed term surface is divided by the information it consumes, not by
"returns a value" versus "has effects." In particular, loads and samples belong
in ordered instructions despite producing data.

| Algebra fragment | Draft methods | Carriers and obligations |
|---|---|---|
| Primitive | `Apply(BoundOperation, data[]) -> R` | Pure data operations; implemented in specimen. |
| Address formation | `Local(LocalSymbol)`, `Field(A,MemberSymbol)`, `Element(A,V) -> AOut` | Exact owner/pointee, address space and access. Formation is not a load; arbitrary swizzle addresses are excluded. Roots and owned member projections are implemented in the specimen; index projection remains draft. |
| Memory read/write | `Load(A) -> VOut`, `Store(A,V) -> S`, `StoreSwizzle(A,lanes,V) -> S` | Read results and stores are ordered. Root writability and data type are checked. Specimen implements address load/store and swizzle store. |
| Buffer | `Length(B) -> VOut`, `Load(B,V) -> VOut`, `Store(B,V,V) -> S` | Buffer category/element/access and u32 index. Length queries have different requirements from mutable-content reads. Load/store implemented in specimen. |
| Texture | `SampleLevel(contract,T,S,U,L) -> R` | Separate texture, sampler, UV and LOD carriers. Exact binder-derived result; implemented in specimen. |
| Derivative | `Derivative(kind,V) -> VOut` | Data-producing but requires shader participation/stage legality. Specimen models the requirement, not quad execution or uniformity proof. |
| Call | `Invoke(FunctionSymbol, Argument<V,A>[]) -> C` | Callee identity is not signature identity. Argument is a sum of data/address, result **types** come from FunctionSig, result **definitions** come from the sequencer. Not implemented by specimen. |

For calls the **draft C# contract** is intentionally invariant where generic
records/rows are involved:

```csharp
interface ICallAlgebra<V, A, F, R>
{
    R Invoke(F function, IReadOnlyList<CallArgument<V, A>> arguments);
}

abstract record CallArgument<V, A>
{
    private CallArgument() { }
    public sealed record Data(V Value) : CallArgument<V, A>;
    public sealed record Address(A Value) : CallArgument<V, A>;
}
```

An instruction sequencer pairs the callee result-type row with freshly allocated
data/address definitions and retains their exact order. A void call allocates
none. It records `Unknown` call requirements until a compiler analysis supplies
a valid summary; recursive summaries are not static properties of a pure math
descriptor. Raw effect records and snapshots are not automatically a checked
whole-function IR.

The **draft sequencer contract**, independent of the algebra's chosen carrier,
is the following. `D` and `AD` stand for fresh data and address definitions;
`V` and `A` stand for uses of already bound definitions.

```csharp
abstract record ResultDefinition<D, AD>
{
    private ResultDefinition() { }
    public sealed record Data(D Definition) : ResultDefinition<D, AD>;
    public sealed record Address(AD Definition) : ResultDefinition<D, AD>;
}

sealed record CallLayer<F, V, A>(
    F Target, ImmutableArray<CallArgument<V, A>> Arguments);

sealed record CallInstruction<F, V, A, D, AD>(
    CallLayer<F, V, A> Invocation,
    ImmutableArray<ResultDefinition<D, AD>> Results);

interface ICallSequencer<F, V, A, D, AD>
{
    CallInstruction<F, V, A, D, AD> EmitCall(
        F function, ImmutableArray<CallArgument<V, A>> arguments);
}
```

These records describe immutable output; they are not by themselves proofs.
The concrete checked builder specializes
`F=FunctionRef, V=DataRef, A=AddressRef, D=DataRef, AD=AddressRef`.
Its constructor owns the function's definition allocator and has no overload
accepting caller-selected result types or definitions. Its binding equation is:

```text
signature = lookup(function).signature
require exact owner/mode/type match(arguments, signature.parameters)
require current source/target profile admits signature.results
results[i] =
  Data(newDataDefinition(owner, t))       when signature.results[i] = Data(t)
  Address(newAddressDefinition(owner,p)) when signature.results[i] = Address(p)
append one call step with those results in index order
```

Allocation happens only after all checks succeed, yields fresh identities in
the caller, and never aliases input definitions. Empty result rows allocate
zero identities. The builder's `CallInstruction.Results` is both the published
definition row and the row returned to the caller; there is no second mutable
result registry. A two-result WASM call gets two definitions; a frexp result
gets one aggregate definition. Current profiles reject unsupported address or
multi-result returns before allocation. The algebra can choose this call-step
carrier, a printer carrier, or an analysis carrier; that freedom does not change
the sequencer's concrete obligations.

`FunctionRef` lookup separately provides the current
`CallRequirement = Unknown | Known(summary)`. The instruction refers to that
function identity rather than freezing a potentially stale summary in the pure
operation catalog. Source provenance and analysis validity are separate
annotations on the call site.

Memory behavior and execution requirements are different coordinates:
`Memory = None/Read/Write/...`, `Participation = None/DerivativeQuad/...`.
They inform, but do not alone prove, legality of reordering or duplication.
Alias analysis and stage/uniformity analysis remain compiler responsibilities.

For `v.zy = w.xy`, first compute the complete RHS as data, then record
`StoreSwizzle(rootAddress(v), [2,1], rhs)`. Do not create a fake core pointer to
shuffled vector lanes. A target can later represent that write as a Slang
swizzle place or lower it into component writes with preserved RHS evaluation.

### 4.5 A real heterogeneous algebra and its combinator

The specimen's texture fragment is:

```csharp
public interface ITextureRead<in T, in S, in U, in L, out R>
{
    R SampleLevel(
        SampleLevelContract contract, T texture, S sampler, U uv, L lod);
}
```

Its compiled adapter maps texture, sampler, UV and LOD in operand order exactly
once, calls the original algebra, then maps its output. It supports a checked
IR builder, capture factory, and tracing interpreter without a Kind projection
cast. `SampleTerm.Map` is derived through `Evaluate` and an adapted capture
factory; it is not a second hand-maintained traversal.

The compiled `IDataTypes`/`IResourceTypes` composition and the texture adapter
demonstrate different concerns: interface composition joins contracts; an
adapter changes carriers; neither silently implements a new operation.
The prototype accepts some additional carrier parameters rather than pretending
UV and LOD have the same semantic sort in every interpreter. The IR interpreter
can still use `DataRef` for both.

`SampleLevelContract` is binder-owned and immutable. It is cached by resource
**types**, not individual resource identities; alternate textures/samplers in
the same arena satisfy it. A foreign resource arena is rejected. Mapped syntax
can be ill-typed, so interpretation into checked IR repeats the relevant checks.
This is why a lawful `Map` is not a proof of valid shader IR.

### 4.6 Control, binding, and optional recursion

The **draft generic control algebra** retains complete transfers:

```csharp
sealed record Transfer<L, V, A>(
    L Target, ImmutableArray<CallArgument<V, A>> Arguments);

interface IControlAlgebra<L, V, A, R>
{
    R Return(ImmutableArray<CallArgument<V, A>> values);
    R Jump(Transfer<L, V, A> target);
    R Branch(V condition, Transfer<L, V, A> whenTrue, Transfer<L, V, A> whenFalse);
    R Switch(V selector, ImmutableArray<Transfer<L, V, A>> cases,
        Transfer<L, V, A> otherwise);
}
```

In this shared-IR proposal `cases[i]` means integer case `i`; the default is a
separate arm. Sparse target switch labels require a distinct keyed target form,
not reinterpreting this ordered array. Empty return rows are void, not Unit.
There is no implicit resource argument alternative.

The specimen's checked `EdgeBinder` demonstrates the concrete boundary:

```text
join parameters = [Data(f32), Address(f32,Local,ReadWrite), Data(f32)]
branch condition:bool
  true  -> join(value, localAddress, value)
  false -> join(value, localAddress, alternate)
```

Both arms remain present; repeated values remain repeated; source order is
preserved. A data-f32 slot cannot accept address-of-f32. Equal label names do
not identify one target. Owner, arity, type, address-space and access mismatches
are rejected.

The **draft function representation** is a frozen declaration plus ordered
blocks, each with an ordered parameter row, ordered instructions, and exactly
one terminator. Nested region containment can be retained separately. Free
labels/value references are bound by the function, not unfolded as child terms.
Block-local constructors do not prove dominance, lexical transfer visibility,
address stability, initialization or whole-graph reachability. The existing
checked region/control passes retain those obligations until explicitly migrated.

Tree expression fragments may use `Fix`/folds. For type nodes the factory can
intern instead of wrap; for SSA it allocates references; for CFG it registers
blocks and edges. These are distinct interpreters of appropriate signatures,
not a universal recursive wrapper or a claim that `cata` solves graph cycles.

### 4.7 Native CIL/WASM and target contracts

These are **draft algebra families** grounded in existing stage requirements;
the specimen does not implement bytecode decoding or a backend.

| Layer | Concrete constructors/method families | What does not belong here |
|---|---|---|
| Raw CIL | `Numeric(CilNumericOpcode)`, `Local(CilLocalOpcode,LocalIndex)`, `Field(CilFieldOpcode,FieldToken)`, `Call(MethodToken,CallMode)`, `Branch(CilPredicate,Offset)`, `Switch(Offset[])`, type-bearing indirect/init/conversion operations. | Do not resolve a metadata token into a shader member or assign call stack effects in the raw decoder. Opcode-specific enums exclude nonsensical operand combinations. |
| CIL envelope/environment | Original opcode encoding, byte range, ordered prefixes, EH regions, method signature, locals and InitLocals. | Unsupported prefixes/EH may be retained and rejected later, never dropped to make the model fit. |
| Bound CIL and stack | Replace token/index carriers with resolved symbols; retain canonical stack categories and explicit pre/post transitions. | CLR u32/bool declarations are not new evaluation-stack categories. Stack duplication does not duplicate a load effect. |
| Native WASM | `Numeric(WasmNumericOpcode)`, indexed locals/functions/types; `Block(BlockSignature,B)`, `Loop(BlockSignature,B)`, `If(BlockSignature,B,B)`, `Br(LabelDepth)`, `BrIf(LabelDepth)`, ordered `BrTable`. | Label depth remains native structured-WASM data; resolving it produces a shared label/transfer, not a globally named depth. Block signature has ordered params and results. |
| Slang target | `Declare`, `Bind`, `Assign`, `If`, `Loop`, `DoOnce`, `Break`, `Continue`, explicit value/void returns; separate operand and place syntax. | No graph analysis in text emission. Core addresses require an explicit proven mapping to target places. |
| SPIR-V-like target | Explicit result/type IDs, operands, declarations, decorations, capabilities, blocks and merge/control instructions. | Sharing operation families does not establish valid SPIR-V environment/legalization. No new support claim. |

The **draft CIL algebra signatures** separate operand categories instead of
putting an arbitrary metadata object beside an opcode:

```csharp
interface ICilNumeric<T, R>
{
    R Arithmetic(CilArithmeticOpcode opcode);
    R Convert(CilConversionOpcode opcode);
    R InitObject(T type);
}

interface ICilStorage<P, L, F, R>
{
    R Argument(CilArgumentOpcode opcode, P parameter);
    R Local(CilLocalOpcode opcode, L local);
    R Field(CilFieldOpcode opcode, F field);
    R Indirect(CilIndirectOpcode opcode);
    R Duplicate();
    R Pop();
}

interface ICilControl<L, M, FS, R>
{
    R Branch(CilBranchPredicate predicate, L target);
    R Switch(ImmutableArray<L> targets);
    R Call(M method);
    R CallVirtual(M method);
    R CallIndirect(FS signature);
    R NewObject(M constructor);
    R Return();
}
```

Leaf opcode types are disjoint enums matching their operand family, not a
shared numeric enum: `CilArithmeticOpcode` preserves add/add.ovf/add.ovf.un,
signed/unsigned division and comparisons; `CilFieldOpcode` preserves
load/address/store and static/instance distinction. Indirect operations
retain their encoded scalar/native/reference variant. Literal forms are
another leaf fragment with their exact bit payload, not an `object` value.

The important carrier instantiations are:

```text
Raw:   T=TypeToken, P=ParameterIndex, L=LocalIndex, F=FieldToken,
       branch L=ByteOffset, M=MethodToken, FS=SignatureToken
Bound: T=CilDeclaredType, P=ParameterSymbol, L=LocalSymbol, F=FieldSymbol,
       branch L=CilLabel, M=MethodSymbol, FS=CilFunctionSignature
```

Here the `L` in storage and control interfaces is local to its fragment: it is
not one shared carrier. In both cases `R` is one layer of the corresponding CIL
term, not a shader value. The raw envelope is
`(OriginalIndex, ByteStart, ByteEnd, Prefixes[], Layer)`; the method environment
owns exception-region ranges, declared signature/locals and `InitLocals`.
Prefix data preserves ordered flags and typed operands such as a constrained
type token. Unsupported combinations stay available for diagnostics.

The binding interpreter maps tokens into symbols and reifies **bound CIL**,
not shader operations. Its result is success with a bound module or a diagnostic
with source site; failure does not publish partially bound declarations.
Only the next analysis computes:

```text
CilTransition = (Pre: CilStackSlot[], Post: CilStackSlot[])
CilStackSlot  = I32 | I64 | F32 | F64 | Object(CilDeclaredType)
              | Value(CilDeclaredType) | ManagedRef(CilDeclaredType)
```

That slot set describes the current repository's admitted model, not every
possible ECMA-335 stack state. Calls use the resolved signature to derive their
transition. Shader-stack lowering then emits a sequence of
`Apply/PushAlias/Duplicate/Drop` layers with typed depth/immediate operands and
derived transitions. Mechanical stack elimination resolves all depths against
one pre-operation snapshot; `dup` copies a reference, never repeats the
definition's effects. Native predicate lowering retains ordered branch and
fallthrough arms.

The **draft WASM families** use native structured bodies rather than CIL offsets:

```csharp
interface IWasmNumeric<R>
{
    R Numeric(WasmNumericOpcode opcode);
    R Constant(WasmLiteral bits);
}

interface IWasmStorage<L, F, R>
{
    R LocalGet(L local);
    R LocalSet(L local);
    R LocalTee(L local);
    R Call(F function);
}

interface IWasmControl<Sig, L, B, R>
{
    R Block(Sig signature, B body);
    R Loop(Sig signature, B body);
    R If(Sig signature, B thenBody, B elseBody);
    R Branch(L label);
    R BranchIf(L label);
    R BranchTable(ImmutableArray<L> cases, L otherwise);
    R Return();
    R Unreachable();
}
```

`WasmLiteral` is a closed sum of supported native bit-payload forms.
`Sig` is a raw inline/type-index block type before binding and an ordered
parameter/result row after binding; `L` is `LabelDepth` before resolution and
a scope-owned native label afterward. `B` may be an immutable instruction
sequence or a deferred construction carrier; it is not a core label reference.

Validation resolves label depth against the lexical control stack. Loop branch
arguments match loop parameters; block/if exit arguments match result rows.
Conditional branch keeps the non-taken stack state specified by the native
operation. Explicit shared-IR transfers are produced only after this binding,
with complete data tuples. The current proposed frontend profile still rejects
multi-values, local.tee and other out-of-profile terms; including constructors
here prevents an encoding dead end, not a change to its whitelist.

The **draft Slang-facing term signatures** distinguish values, places and
statement bodies:

```csharp
interface ITargetExpressions<V, P, F, R>
{
    R Value(V value);
    R Read(P place);
    R Primitive(ClosedPrimitive operation, ImmutableArray<V> operands);
    R Call(F function, ImmutableArray<V> arguments);
}

interface ITargetPlaces<V, P, L, Param, M, R>
{
    R Local(L local);
    R Parameter(Param parameter);
    R Member(P root, M member);
    R Index(P root, V index);
    R Components(P root, ImmutableArray<int> lanes);
}

interface ITargetStatements<E, P, D, B, R>
{
    R Declare(P place);
    R Bind(D definition, E expression);
    R Assign(P place, E value);
    R GetDimensions(E buffer, P count, P stride);
    R If(E condition, B whenTrue, B whenFalse);
    R Loop(B body);
    R Once(B body);
    R Break();
    R Continue();
    R ReturnValue(E value);
    R ReturnVoid();
}
```

The nominal type names in this draft (`L`, `Param`, `M`) are carrier
parameters, not reflection types. Choose `V=TargetValueRef`,
`P=TargetPlace`, `E=TargetExpr`, `D=TargetValueRef`,
`B=ImmutableArray<TargetStatement>`, `R=TargetStatement` for statement reification.
Expression reification instead chooses `R=TargetExpr`. The primitive expression
form uses already materialized value operands; effectful subexpressions must be
bound into prior statements, not duplicated by printing.

Address-to-place translation is a compiler-produced partial mapping:
`AddressRef -> TargetPlace` for aliases whose root and lifetime are established;
failure is an explicit unsupported lowering, not a guessed variable name.
Assignability and read-only-root validation belongs at checked target
construction. `GetDimensions` is a dedicated ABI statement with real output
places, while logical pure calls retain their declared result shape.

A Slang `GetDimensions(buffer, out count, out stride)` produces two target
definitions even when the source operation was just `buffer.Length`. Conversely
an intrinsic such as `frexp` may have one logical product result lowered through
out parameters. Target ABI result bindings are not a reason to change the
source mathematical result shape.

Literal forms, conversions, comparisons, and exceptional behavior stay
language-specific until bound to a contract that expresses them exactly.
Adopting one `Add` enum across checked CIL, WASM wrapping arithmetic and floating
arithmetic without such a contract is explicitly rejected.

### 4.8 Authoring and generator binding

The v1 recommendation is an **operation-family marker plus exact signature
binding**, while leaving enum versus marker-type spelling undecided. Neither
spelling is the canonical runtime type/operation object.

```text
public CLR method + explicit intrinsic family marker
  -> map receiver/parameter/result metadata through the type context
  -> validate instance receiver and in/ref/out modes
  -> bind the operation's relational signature
  -> compare the actual CLR result with the inferred result
  -> register method identity -> binding plan
```

The generator uses that same bound definition to produce its CLR signature and
body selection; the registry does not maintain another handwritten overload
table. Generated sources remain wholly generator-owned. A handwritten
`distance(vec3,vec3)->vec3` or `refract(vec3,vec3,vec3)` must fail registration,
before the method's declared signature is replaced by a trusted declaration.
Closed generic witnesses may supply a definition, but do not certify the
attributed CLR member by themselves.

Receiver adapters are explicit: an instance vector getter taking `this` byref
is not automatically a pure vector projection. Resource wrapper receivers
resolve to resource bindings; they do not become user data.

### 4.9 What the candidate rejects

- A separate interface/type for every `(operation, scalar, rank)` overload:
  this reproduces the cross-product instead of expressing shape relations.
- A universal `Apply(string, object[])`: it loses identity, carrier categories,
  exact results and named rejection boundaries.
- `Place` as a synonym for both core byref and target l-value syntax.
- "Effect instruction means no result"; memory reads and calls disprove it.
- Encoding all nominal structs, lengths and metadata as CLR generic parameters.
- A generic `Map`/`DiMap` that silently preserves stale type or analysis proofs.

The cost of the chosen runtime binder is deliberate: some relationships move
from C# constraints to checked boundaries. The benefit must be measured against
the concrete consumers and failure cases below, not against a toy expression.

## 5. Modeling evidence and required coverage

`P` means executed by the standalone specimen; `D` means a concrete draft trace
or contract, not executed production evidence. A `P` does not mean GPU execution.
This is an acceptance matrix for the design, not a new feature-support table.

| Case grounded in repository needs | Required modeled result / rejection boundary | Evidence |
|---|---|---|
| Mandelbrot vector arithmetic and loop [R11] | Scalar/vector lifting, vector constructors/components, dot reduction and multiple loop-carried values must fit without per-overload visitors. | D: full shader mapping below; P: vector geometry chain, explicit splat and relational binders. Entire shader is not executed by the specimen. |
| Geometry operations from #243 | Refract retains scalar eta; dot/distance return scalar; ldexp exponent has matching integer shape. Reject vector eta, mixed ranks and unsupported broadcasts at operation binding. | P: binder and emitted geometry steps. |
| Nominal uniform structure [R12] | Repeated source identity resolves once; different declarations with identical name/layout remain distinct; ordered metadata survives. Uniform reads and local writes are different operations. | P: nominal identity, member metadata, owned field-address/load, foreign-member rejection, resource exclusion and local write/uniform-write rejection. D: CLR source-symbol interning. |
| `v.zy = w.xy` | Snapshot RHS, preserve destination lane order; reject duplicate destination lanes; never invent an address to a shuffled aggregate. | P: root+lane+value store representation and rejection; D: RHS read/projection lowering. |
| RO/RW buffer compute [R13] | Guarded u32 index, result-bearing content read, pure math, resultless store. Preserve operation order; reject RO stores and foreign owners. | P: load/store effects and owner/access/index checks; D: guards/loop execution and bounds semantics. |
| Texture sample and derivative [R14] | Separate texture/sampler/UV/LOD carriers; infer vec4f32; alternate same-arena resource valid, foreign resource rejected. Derivative requires participation rather than pretending to be a memory read. | P: checked sample, carrier adapters and requirement field. D: stage/uniformity and quad semantics. |
| Shared join with mixed data/address tuples [R4] | Distinct true/false arms, repeated arguments, exact slot sorts, nominal labels and function owners. | P: tuples and boundary rejections; D: dominance, stability, scope and loop validation. |
| Matrix/array type stressors | `Mat<2,3>*Mat<3,4>->Mat<2,4>`; array length 37 is data in the type description; invalid inner dimension/zero length fails. | P: type algebra reification and binder; not current matrix/array backend support. |
| Aggregate versus multiple results | Frexp returns one generated product with exactly fraction/exponent fields, canonical across binders. WASM results remain an ordered row. | P: frexp product identity and field types/order; D: multi-result calls and target out-parameter lowering. |
| Intrinsic mismatch | Wrong CLR return, receiver mode or parameter relation fails registration, not late emission. | D: section 4.8; not implemented by the specimen. |
| Native CIL/WASM preservation | Prefixes, EH, metadata identity, stack transitions and native structured labels survive until their explicit admission/lowering boundary. | D: section 4.7, current stage references R1-R3; no new bytecode path. |
| Semantic operation identity | Checked CIL arithmetic must not acquire a wrapping operation merely because its types match. | P: explicit wrapping contract shared by unchecked CIL/WASM and checked-add rejection; D: full float contracts and target conformance. |

### Non-toy trace: Mandelbrot and uniform access

For [MandelbrotDistanceShaderModule][R11], type binding must handle:

```text
fragCoord:vec4f32 -> extract.xy -> vec2f32
2*f.xy            -> splat(2) then componentMultiply
... - resolution  -> componentSubtract(vec2,vec2)
z/dz updates      -> component extracts + scalar operations + compose.vec2
dot(z,z)          -> f32, not vec2
sqrt/log/pow/cos   -> float-family signatures, not arbitrary matching names
loop              -> explicit z/dz/m2/di/i definitions and ordered transfer tuples
vec4(col,1)       -> compose(vec3f32,f32) with component total 4
```

The v1 model can describe these shapes; completing their CPU definitions and
numeric contracts remains #243 work. Loop participation, locals, branch arms and
actual targets remain compiler obligations, not an implicit recursive evaluator.

For [SimpleStructUniformShaderModule][R12], the source uniform root has the
nominal `OurStruct` type; `scale` and `offset` are owned members of that
declaration. The draft core sequence is `FieldAddress -> Load -> pure math`;
`color` is independently loaded as vec4. A member token from an equal-shaped
different struct must be rejected. Assigning a local vector's swizzle is legal;
writing through the uniform root is not. Offsets are intentionally not guessed
from logical `ByteSize`.

### Executable specimen and limits

Run with the existing pinned SDK, without adding packages or project references:

```sh
nix develop --builders '' --command \
  dotnet run -p:ImportDirectoryPackagesProps=false \
  docs/clsl/encoding/EncodingContractProbe.cs
```

The probe tests actual contracts, including non-identity input **and output**
`DiMap` composition, factory-derived `Map`/evaluation agreement and ordered map
invocations. It also rejects malformed mapped texture syntax at the builder.
Standalone model exceptions include the rejecting boundary.

Recorded output on 2026-10-02:

```text
Encoding contract probe passed 78 semantic checks.
Deliberately unimplemented: calls, dominance/stability analysis, target alias analysis.
```

These are representative executable checks, not a proof of algebraic laws for
all callbacks, or a substitute for production compiler and hardware evidence.

The specimen is not a production API: its `int` array-length ceiling, scalar
subset, single-result pure operations, atomic struct definitions, local/uniform
addresses and sampled-2D resource profile are explicit limits. Raw descriptions,
steps and layers may be constructed for observation; only its checked factories
and builders establish the tested local guarantees. Constructors internal to
this standalone assembly are a trusted implementation boundary, not a defense
against hostile code in the same assembly.

Not demonstrated: full numerical evaluation/parity, generalized function calls,
source attribute registration, recursive declarations, function-level graph
admission, dominance, address stability, target aliasing, native CIL/WASM codecs,
or Slang/SPIR-V output. These are concrete remaining contracts above, not claimed
successes hidden behind the probe count.

## 6. Existing abstractions to learn from

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

## 7. ILGPU: pinned comparison, not a chosen architecture

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

## 8. Ordered discussion and experiment ledger

No encoding decision is approved by this initial document. Each row is a small
design question; implementations will be split further where independently
shippable. Do not open implementation PRs for the whole table automatically.

| ID | Status | Question and required concrete evidence |
|---|---|---|
| D01 | v1 proposed; review | Sections 4-5 replace the toy example with actual type/operation/address/resource/control requirements and language-stage contracts. Decide these boundaries using the coverage matrix. |
| D02 | Partial executable model; open | Canonical types, nominal structs, metadata, arrays/matrices and owner checks are exercised. Decide forward declaration, full scalar/length profile and source identity import. |
| D03 | Partial executable model; open | Type algebra reification and heterogeneous texture `DiMap`/factory-derived Map compile and execute. Broader composed interpreters remain to be designed and assessed. |
| D04 | Partial executable model; open | Geometry, explicit broadcast, reduction, ldexp, frexp and matrix product binders are exercised. Constructors, conversions, full builtin families and numeric semantics remain. |
| D05 | Open | Authoring identity: compare enum ID, family marker and closed witness attributes. Show generated and handwritten call sites, exact CLR signature mismatch diagnostics, and where validation runs. |
| D06 | Open | Representation/recursion: contrast one expression tree, an interned DAG, nominal type references, a shared join and a loop. Specify strictness, identity, traversal and error behavior; ordinary cata is not the CFG solver. |
| D07 | Open | Language adapters and target capability: show CIL checked versus wrapping arithmetic, WASM trap/control behavior, and shader-only effects without equating names or broad families. |
| D08 | Open | Migration/generation: select one approved vertical slice, migrate every affected caller, regenerate deterministically including stale-file detection, and preserve compiler/CPU/target evidence. |

The 2026-10-02 requirement supersedes the earlier i32-add-led agenda: every
proposal must account for the full modeling matrix, not claim architectural
adequacy after a toy example. A small implementation slice is still appropriate;
a small *requirements model* that omits existing IR needs is not.

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

Only code explicitly linked to the executable specimen has been exercised.
Draft signatures and traces are not implementations. Continue using existing
infrastructure and record actual outputs without treating a model probe as
production compiler or GPU evidence.

## 9. Related work and scope coordination

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
- **R11:** [Mandelbrot vector/control example][R11].
- **R12:** [Nominal uniform-structure example][R12].
- **R13:** [Writable structured-buffer cases][R13].
- **R14:** [Texture sample example][R14] and
  [cooperation/derivative cases](../../DualDrill.CLSL.Test/CooperationAdmissionTests.cs).

[R11]: ../../DualDrill.CLSL.Test/ShaderModule/MandelbrotDistanceShaderModule.cs
[R12]: ../../DualDrill.CLSL.Test/ShaderModule/SimpleStructUniformShaderModule.cs
[R13]: ../../DualDrill.CLSL.Test/WritableStructuredBufferTests.cs
[R14]: ../../DualDrill.CLSL.Test/ShaderModule/TextureSampleLevelShaderModule.cs

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

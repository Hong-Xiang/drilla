# Shader features: current evidence and implementation roadmap

Snapshot: 2026-09-20, source baseline
`fb7304a30b9102ac770ba7547512dd9bdf65c9d3`. Inventory issue: #160; PR: #162.
The accompanying [API examples](./shader-feature-proposals.md) are proposals,
not implemented interfaces. This slice changes documentation and tests only.

> **Current status:** bounded uniform buffer layout and typed reflection were
> implemented after this historical snapshot. See
> [Uniform buffer layout](./uniform-layout.md). The inventory below is preserved
> as evidence of the earlier baseline.

**Current useful compiler subset:** vertex/fragment scalar/vector signatures,
read-only uniform struct fields, ordinary direct helper calls, and a selection
of scalar/vector arithmetic and math. This is not a complete shader language.
Buffer/texture resource APIs, compute execution, layout contracts, and GPU
cooperation must not be inferred from similarly named declarations.

## Evidence boundaries

The actual public path is `CLSLCompiler.Parse/Compile/Emit`:

```text
C# reflection -> RawCilFunctionBody -> Pre/typed shader stack -> value CFG
-> local promotion/control facts -> RegionFunctionBody
-> FunctionToOperation/StablePointer passes -> Slang target AST -> Slang text
-> external slangc for WGSL and, separately, reflection JSON
```

See [public compiler][compiler], [stage pipeline][pipeline], and
[Slang invocation][slang-service]. WGSL is a Slang target, not a second direct
CLSL emitter. Public targets also include diagnostic IR output. The incomplete
[SPIR-V emitter][spirv] is not a public target; the compute-only Wasm work
(#116/#118) is a separate profile, not evidence of GPU resource support.

Status vocabulary used **at each boundary** below:
`C` = exercised by a compiler/target assertion; `P` = partly wired;
`D` = declaration only; `M` = no live path found in the audited scope;
`U` = not established by this evidence. A Slang/WGSL `C` means compilation or
reflection, **not GPU execution**. `M` is a bounded inventory result, not a
claim that the repository contains no similarly named host graphics type.

The source audit covers `DualDrill.CLSL.Language`, `DualDrill.ILSL`,
`DualDrill.Mathematics`, `DualDrill.CLSL.Test`, and `Shared/Shaders` C# sources.
The separate `DualDrill.Graphics` host API is not a shader-language surface.
Runtime integration is described separately below.

## Feature matrix

References name exact files; line ranges identify the baseline evidence.
`T` refers to `ShaderFeatureCharacterizationTests`; `E` to
`RuntimeReflectionCompilerE2ETests`. See [runnable evidence](#runnable-evidence).

| Feature / variants | C# surface and reflection collection | CIL, precise types and shared operations | Slang AST/text -> WGSL | Reflection / decisive evidence |
|---|---|---|---|---|
| Uniform scalar/vector/struct reads | C: `[Uniform, Group, Binding]` fields collected [R:125-140,492-499][parser] | C: typed module variable, load and member projection | C: `ConstantBuffer<T>`, `vk::binding`; target compiles to WGSL | C: E `SimpleUniformShaderShouldWork` checks binding/type/members; **not offsets or host bytes** |
| Read-only/read-write structured storage buffers | D: `[Read]`/`[ReadWrite]` markers are not address-space attributes [A:30-36][attributes] | M: no storage space or buffer type/load/store path [AS:5-12][spaces] | M: no buffer-specific lowering/emission | M: internal helper assumes every variable is uniform [RF:239-284][reflection] |
| Fixed/runtime arrays, element indexing/length | D: two array records do not implement `IShaderType` [AR:1-9][arrays]; CLR arrays become `OpaqueType` [R:87-108][parser] | M: `newarr`, `ldlen`, `ldelem*`, `stelem*`, `ldelema` reject [IL:202-225][cil] | M: `AccessChainOperation` explicitly rejected [L:376-380][lowering] | T: ordinary `int[]` read fails in Pre, distinct from hand-built IR rejection |
| Buffer layout/alignment/stride | D/P: `[Align]` retained on members; no offset/stride contract [R:469-481][parser] | M: scalar-product byte sizes are not target layout; no canonical layout result | P: target chooses layout; member attributes erased [S:74-79][emitter] | P: Slang JSON available; internal `MinBindingSize=0` [RF:251-256][reflection] |
| Sampled 1D/2D/3D/cube/array textures | D: legacy `ITexture2D<T>.Sample`, not a registered intrinsic [SY:32-39][syntax] | M: reference types are opaque; abstract `Sample` has no CIL body | M: no typed resource declaration or sampling lowering | T: opaque mapping and exact recursive-collection rejection |
| Storage/depth/multisampled textures | M: no live declaration surface | M: no dimension/sample/format/access type or operation | M: no load/store/depth lowering | M: no resource reflection branch |
| Sampler/comparison sampler; load/sample/store/gather | D: legacy `ISampler` only; M: comparison sampler | M: no typed operations, explicit LOD/gradients/offsets or comparison reference | M: no target path; upstream capability is not CLSL support | M: no sampler/texture binding information |
| Binding group/index | C/P: retained integer attributes, but only an address-space field is collected [R:492-499][parser] | P: no pair uniqueness/range validation | C/P: both attributes emit `[[vk::binding(binding, group)]]`; either alone silently omits it [S:112-126][emitter] | C: simple uniform pair; P: internal helper collapses groups and assumes a binding [RF:239-290][reflection] |
| D3D register class/space | M: no explicit portable mapping policy | M: no distinct mapping contract | U: upstream Slang has mappings; no public CLSL D3D path | U: must not equate `t0`, `s0`, `b0`, `u0` with one Vulkan binding |
| Vertex/fragment scalar/vector interfaces | C: stage roots; parameter builtin/location collected [R:47-56,532-539][parser] | C/P: semantic input pointers; no stage/type/duplicate validator | C: narrow fixtures; P: location mapping is stage-insensitive [S:323-356][emitter] | C: E `MinimumTriangleShaderShouldWork`, uniform fixture |
| Builtin position / vertex index | C: typed enum attributes | C: signatures pass current pipeline | C: `SV_POSITION`, `SV_VertexId` -> WGSL builtins | C: E minimum triangle |
| Instance/sample/front-face/depth and compute builtins | D: enum names exist [BI:3-20][builtins] | P: metadata collected without legality checks | M: all but position/vertex_index reject [S:336-344][emitter] | T: `instance_index` exact emitter rejection |
| Struct entry interfaces, locations/interpolation | P: members retain attributes [R:469-481][parser]; M: interpolation surface | P: plain member access works, not an interface ABI | M/P: member annotations silently erased [S:74-79][emitter] | T: retained attributes but no `SV_POSITION`/`TEXCOORD0` in emitted struct; not valid target acceptance |
| Compute entry/workgroup size | D/P: `[Compute]` is collected; M: workgroup-size attribute | M: no workgroup-size validation/IR contract | M: `ComputeAttribute` rejected in emitter | T: empty compute entry reaches emitter and rejects |
| Function/uniform/input/output address spaces | P: typed spaces exist; locals/uniforms/semantic inputs use some of them | P: intrinsic pointer matching ignores address-space differences [F:94-100,125-131][function-pass] | P: uniform has distinct emission; stage I/O uses semantics | No claim of enforced memory access modes |
| Workgroup/private storage, invocation/workgroup/subgroup IDs | M: no authoring surface for private/workgroup storage; ID names D only | M: no storage model, scope or memory-order contract | M: no usable target path | Missing, not inferred from WebGPU host enums |
| bool/i32/u32/f32 | C: builtin runtime mappings [B:52-79][symbols] | C/P: typed arithmetic/comparison/conversion; not all CIL ops | C: selected scalar tests and public bool-call tests | No claim that bool is legal in host-shareable WGSL buffers |
| i8/u8/i16/u16/i64/u64/f16/f64 | D/P: runtime types registered; some CIL normalization | P: narrower/wider typed operations do not imply portable target support | U: only `f32/i32/u32` aliases explicitly emitted [S:371-379][emitter]; no matrix of target capability tests | Target-specific evidence required for each type/op |
| Vectors, constructors, swizzles | C/P: generated rank 2/3/4 types; hard-coded System.Numerics mappings [B:67-79,130-144][symbols] | C: broadcast/component/swizzle and construction; not every CLR constructor | C/P: f32 vector paths exercised; broad generated overload set unproven | `ParseBodyTest`; target pointer projection cases [TA:845-958][target-tests] |
| Matrices, orientation, matrix intrinsics | D: `MatType` methods throw; no runtime mapping [MT:12-29][matrices] | M: no working matrix construction/lowering | U/M: no exercised emission; determinant/transpose overload lists empty [SF:320,391][functions] | Missing ABI/orientation, not support implied by type name |
| Plain user structs | P: value types become `StructureType`, fields/properties reflected | C: uniform field reads; P: general construction/getters (`initobj`/`this`) unsupported | C: plain uniform members; P: construction and interface semantics | E uniform fixture; not arbitrary CLR struct compatibility |
| Scalar casts / vector casts | P: selected `conv.*` opcodes; generated vector conversion attribute | P: scalar conversion typed; vector attribute `GetOperation` throws [CA:20-25][conversion] | C/P: bool-call conversion exercised; not all casts | No unchecked overflow/bitcast policy established |
| Common math | C/P: name/signature registration of compatible `DMath` methods [B:102-128][symbols] | P: ordinary typed calls; declared overload set exceeds evidence | C/P: helper/math corpus, `mix` renamed `lerp`; no capability table | E Mandelbrot/Raymarching tests exist; not all run for this inventory |
| Direct calls / inlining | C: recursively collect reachable static methods; builtin boundary stops collection [R:180-214,519-529][parser] | C: typed calls and bool stack/declaration conversion | C: functions remain calls, not an inlining guarantee | Existing public bool-call probe run with this slice |
| Generics/interface/virtual dispatch | P/U: reflection records closed generic arguments; no specialization pass [R:310-320][parser] | M: `callvirt`, `calli`, `constrained.` reject; abstract body collection fails | U: simple closed generics uncharacterized; interface-based sketch is not compiler evidence | `CompileTimePolymorphicTest` only constructs CLR objects |
| Derivatives (`dpdx*`, `dpdy*`, `fwidth*`) | D/P: generated methods registered by name | P: ordinary calls, no stage/uniformity/quad contract | U: forwarding spelling is not target acceptance; no derivative probe here | #154 owns participation semantics; no runtime guarantee |
| Atomics/barriers/memory semantics | M in bounded audit | M: no atomic type/op, order/scope, execution barrier model | M: no exercised lowering | Must define shared memory and participation first |
| Subgroups, discard, helper invocations | M in bounded audit | M: no effect/participation representation | M: no exercised lowering | Specification/executable examples before production guarantees (#154) |
| Miscellaneous annotations | D: `[ShaderPrimitiveType]` does not drive runtime mapping; `[Align]`, access markers not enforced | P: accepted metadata may have no semantic effect | P: unknown function attributes reject; variable/member ones can be ignored | Host-only `VertexStepModeAttribute` is not a shader attribute |

## Runnable evidence

The new [characterization tests][probes] use ordinary C# plus existing compiler
entry points, not fabricated target IR. They intentionally assert current
rejection or metadata loss; a pass means the limitation is reproduced, **not**
that the feature works. When a feature lands, replace its characterization
with a positive contract in that owning slice.

The same selected run includes existing public triangle/uniform compilation
and bool-call Slang/WGSL probes:

```sh
nix develop --builders '' --command dotnet test \
  DualDrill.CLSL.Test/DualDrill.CLSL.Test.csproj -c Debug --no-restore \
  --filter 'FullyQualifiedName~ShaderFeatureCharacterizationTests|FullyQualifiedName=DualDrill.CLSL.Test.RuntimeReflectionCompilerE2ETests.MinimumTriangleShaderShouldWork|FullyQualifiedName=DualDrill.CLSL.Test.RuntimeReflectionCompilerE2ETests.SimpleUniformShaderShouldWork|FullyQualifiedName=DualDrill.CLSL.Test.BooleanCallTests.BooleanCallsCompileThroughPublicSlangAndWgslApis' \
  --logger 'console;verbosity=detailed'
# Repeat exactly with -c Release for actual optimized CIL.
```

Start with `--no-restore`; only restore if assets are missing. `slangc` comes
from the pinned Nix shell. No server, native smoke test or GPU process is
needed. See [captured inputs/outputs](./shader-feature-evidence.md) for exact
observations and configuration results.

Positive uniform input is the existing [SimpleStructUniformShaderModule][uniform]:
`OurStruct { vec4f32 color; vec2f32 scale; vec2f32 offset; }`,
`[Uniform, Group(0), Binding(0)] static readonly OurStruct ourStruct`,
with vertex reads and fragment `return ourStruct.color`. The public test asserts
Slang reflection `constantBuffer`/`struct`, binding index 0, and WGSL
`@group(0)`, `@binding(0)`, `var<uniform>` and member names. It does **not**
assert a host upload layout, field offsets, buffer size or pixel result.

## Runtime integration is a separate boundary

Slang reflection JSON is not the internal WebGPU bind-group helper.
`ShaderModuleReflection` [RF:239-290][reflection] hard-codes uniform buffers,
ignores groups, assumes binding attributes and reports minimum size zero.
It is not suitable evidence for general storage/texture binding support.
The compiler server exposes compile/reflection endpoints, not automatic
resource allocation/binding. No runtime provider or renderer is changed here.

An existing native resource-free triangle smoke test separately exercises GPU
pixels; it was **not run** here and proves neither uniforms nor other resources.
No runtime end-to-end resource, layout, derivative or convergence claim is made.
Generated CLR `vec3f32` stores `Vector128<float>` while IR `VecType.ByteSize`
is 3 * 4: neither value alone defines a portable shader-buffer ABI.

## Prioritized independently shippable roadmap

These are proposed follow-ups for coordinator review, not newly opened issues
or permission to publish an API. Every slice retains shared typed IR and puts
target legalization before the syntax-only emitter.

| Priority / slice | Dependencies and owner boundary | Exact acceptance example | Human agreement before implementation |
|---|---|---|---|
| P0: reject incomplete bindings and lost interface annotations | Existing declarations; #115 target boundary | Duplicate `(0,0)`, one missing coordinate and annotated struct member give stable diagnostics; existing uniform/triangle still compile | Diagnostic policy for currently accepted-but-ignored input |
| P0: uniform layout/reflection contract | Existing uniform path; reuse #111 address/local distinctions | [Uniform proposal](./shader-feature-proposals.md#3-uniform-layout-and-reflection): 32 bytes, offsets 0/16/20/24, binding `(1,2)`; reject bool/nested arrays outside profile | Canonical layout vs target-specific layout; host serialization; reflection API shape |
| P1: compute signature and builtin validation | #114 typed frontend; #115 lowering | `[Compute, WorkgroupSize(64,1,1)]`, `global_invocation_id:vec3<u32>` -> Slang/WGSL compute entry; wrong stage/type/zero size rejects | Attribute spelling, target/device limit boundary |
| P1: scalar structured-buffer declaration/index/load/store | Compute slice and approved binding/layout contracts | [Buffer proposal](./shader-feature-proposals.md#1-structured-buffer-compute): `[1,3,5] -> [2,6,10]`, lengths/bounds and readonly-store negative cases | Resource wrappers, access types, length/index width, out-of-bounds semantics |
| P1: sampled 2D f32 texture + ordinary sampler with explicit LOD | Typed resource collection/bindings and #115 | [Texture proposal](./shader-feature-proposals.md#2-texture-and-sampler-fragment): sample-level typed op -> `.SampleLevel`/`textureSampleLevel`; reject wrong dimension/sampler | Filtering/sample-type and binding ABI; explicit capability failures |
| P2: implicit LOD/derivatives and interface struct semantics | Texture slice; #113/#117 control facts/scopes; #154 participation specification | Unconditional fragment sample; vertex sample and unproven derivative-uniform control produce stated outcomes | Enforcement versus diagnostic policy; no assumed GPU reconvergence |
| P2: fixed arrays/matrices and additional storage element types | Layout profile, element places; #111 promotion eligibility | Matrix orientation/stride and array layout round trip; typed indexing without erasing access/address space | Supported shapes/layout profiles before generated C# wrappers |
| Later: storage/depth/multisample textures, atomics/workgroup/subgroups | Separate resource-format and memory/effect contracts; #154 | Per-operation target validation plus independent participation examples, not scalar-only equivalence | Memory scopes/order, helper/discard behavior, target capability and runtime validation policy |

#112 owns cleanup of verified legacy paths; keep `Syntax.cs` sketches distinct
from live APIs rather than silently promoting them. #111-#118 provide the
existing compiler stage/value/control/target boundaries, not a competing
resource framework. #153 owns scalar negation; #155 owns switch, which must
remain explicit multiway shared-IR control. This inventory neither flattens
switch nor changes either implementation. #154 owns GPU semantic examples;
scalar CFG equivalence and successful target compilation cannot prove them.

[compiler]: ../../DualDrill.ILSL/CLSLCompiler.cs
[pipeline]: ../../DualDrill.ILSL/Frontend/CilCompilerPasses.cs
[slang-service]: ../../DualDrill.ILSL/SlangService.cs
[spirv]: ../../DualDrill.ILSL/Backend/SPIRVEmitter.cs
[parser]: ../../DualDrill.ILSL/Frontend/RuntimeReflectionParser.cs
[attributes]: ../../DualDrill.CLSL.Language/ShaderAttribute/ShaderAttribute.cs
[spaces]: ../../DualDrill.CLSL.Language/Symbol/AddressSpace.cs
[arrays]: ../../DualDrill.CLSL.Language/Types/ArrayType.cs
[cil]: ../../DualDrill.ILSL/Frontend/CilInstructionInfo.cs
[lowering]: ../../DualDrill.ILSL/Backend/SlangTargetLowering.cs
[reflection]: ../../DualDrill.ILSL/Reflection/ShaderModuleReflection.cs
[emitter]: ../../DualDrill.ILSL/Backend/SlangEmitter.cs
[syntax]: ../../DualDrill.ILSL/Syntax.cs
[builtins]: ../../DualDrill.CLSL.Language/ShaderAttribute/BuiltinAttribute.cs
[function-pass]: ../../DualDrill.CLSL.Language/Transform/FunctionToOperationPass.cs
[symbols]: ../../DualDrill.ILSL/Frontend/SymbolTable/SharedBuiltinSymbolTable.cs
[matrices]: ../../DualDrill.CLSL.Language/Types/MatType.cs
[functions]: ../../DualDrill.CLSL.Language/ShaderFunction.cs
[conversion]: ../../DualDrill.CLSL.Language/ShaderAttribute/ShaderRuntimeMethodAttribute.cs
[target-tests]: ../../DualDrill.CLSL.Test/SlangTargetAstTests.cs
[uniform]: ../../DualDrill.CLSL.Test/ShaderModule/SimpleStructUniformShaderModule.cs
[probes]: ../../DualDrill.CLSL.Test/ShaderFeatureCharacterizationTests.cs

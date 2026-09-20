# Proposed typed C# resource slices

**RESOURCE DESIGN.** The compute entry-point subset (`WorkgroupSize` and
`global_invocation_id:vec3u32`) is implemented. Resource wrappers, resource
operations, layout and reflection records below remain proposed contracts for
the [inventory roadmap](./shader-feature-inventory.md). Resource target snippets
describe intended semantics, not captured compiler output.

## Thin API and type rules

Reuse `IShaderType`, typed declarations, the existing attribute collection
boundary and registered intrinsic mechanism. Extend those only for an accepted
vertical slice; do not implement a backend-specific wrapper framework.
Generic C# wrappers are a proposed authoring surface, not a request for arbitrary
.NET generics, allocation, interface dispatch or virtual calls in shaders.
The reflection frontend must resolve each supported **closed** wrapper and its
intrinsic operations to precise shader types before CIL lowering.

The following is **PROPOSED IR notation**, not current constructor names:

```text
Binding = (group:u32, binding:u32)              // validated unique per module
Buffer<T, Access>                             // T is a checked shader data type
Texture2D<SampleType>                         // opaque resource, not a value struct
Sampler<Kind>                                // ordinary or comparison
LoadElement(Buffer<T, Read|ReadWrite>, u32) -> T
StoreElement(Buffer<T, ReadWrite>, u32, T) -> unit
Length(Buffer<T, Access>) -> u32
```

Access, address space and sample type are semantic type information, never
unchecked target strings. A store cannot accept a readonly buffer. No resource
may silently become `OpaqueType` or an ordinary struct. Restrict the first
buffer slice to `f32` elements and the first texture slice to non-arrayed,
single-sampled, filterable-f32 2D resources plus ordinary sampler.
Additional variants require their own operation signatures and capability tests,
not optional untyped bags. Static fields are shader declarations; resource
objects are not allocated by executing CLR field initializers.

## 1. Structured-buffer compute

**PROPOSED RESOURCE C#** (the wrappers and length/index operations remain new;
the compute signature attributes are implemented):

```csharp
sealed class DoubleValues : ISharpShader
{
    [Group(0), Binding(0)]
    static StructuredBuffer<float> Input;

    [Group(0), Binding(1)]
    static RWStructuredBuffer<float> Output;

    [Compute, WorkgroupSize(64, 1, 1)]
    public static void Run(
        [Builtin(BuiltinBinding.global_invocation_id)] vec3u32 id)
    {
        uint i = id.x;
        if (i < Input.Length && i < Output.Length)
            Output[i] = Input[i] * 2.0f;
    }
}
```

**PROPOSED IR**:

```text
Input  : Buffer<f32, Read>      binding=(0,0) address=Storage stride=4
Output : Buffer<f32, ReadWrite> binding=(0,1) address=Storage stride=4
Run    : (global_invocation_id:vec3<u32>) -> unit
         stage=Compute workgroup=(64,1,1)
i:u32 = component(id, 0)
if i < Length(Input) && i < Length(Output):
    x:f32 = LoadElement(Input, i)
    StoreElement(Output, i, mul<f32>(x, 2.0))
return
```

The buffer element sequence is runtime-sized. It is a storage-buffer store
type, not a uniform/local runtime array; when wrapped in a target structure
its runtime array must be the last member. First-slice length/index are u32,
stride/alignment are 4 bytes, and binding length must be a multiple of 4.
Reflection records readonly-storage versus storage plus stride and binding,
not the runtime element count. Runtime binding byte lengths determine counts.

**INTENDED SLANG LOWERING**:

```slang
[[vk::binding(0, 0)]] StructuredBuffer<float> Input;
[[vk::binding(1, 0)]] RWStructuredBuffer<float> Output;
[shader("compute")]
[numthreads(64, 1, 1)]
void Run(uint3 id : SV_DispatchThreadID)
{
    uint inputCount, inputStride, outputCount, outputStride;
    Input.GetDimensions(inputCount, inputStride);
    Output.GetDimensions(outputCount, outputStride);
    if (id.x < inputCount && id.x < outputCount)
        Output[id.x] = Input[id.x] * 2.0f;
}
```

**INTENDED WGSL SEMANTICS** (Slang may choose different wrapper names):

```wgsl
@group(0) @binding(0) var<storage, read> inputValues: array<f32>;
@group(0) @binding(1) var<storage, read_write> outputValues: array<f32>;
@compute @workgroup_size(64, 1, 1)
fn Run(@builtin(global_invocation_id) id: vec3<u32>) {
    if (id.x < arrayLength(&inputValues) && id.x < arrayLength(&outputValues)) {
        outputValues[id.x] = inputValues[id.x] * 2.0;
    }
}
```

**FUTURE ACCEPTANCE:** input `[1,3,5]`, output initially `[-1,-1,-1]`,
one 64-invocation workgroup produces `[2,6,10]`. Output length 2 gives `[2,6]`;
extra invocations do not access either buffer. Require distinct, nonoverlapping
input/output bindings and dispatch y=z=1 for this example, so each output has
one writer. Do not claim target out-of-bounds behavior or race freedom generally.
Reject stores to `Input`, duplicate binding pairs, non-f32 first-slice elements,
zero workgroup dimensions, and a builtin with wrong stage/type.
Host allocation, dispatch and readback are separate future runtime acceptance;
the first compiler slice must validate operations, emitted targets and reflection.

## 2. Texture and sampler fragment

**PROPOSED C#** (`Texture2D<float>` denotes f32 *sample components*; a sample
returns `vec4f32`, not scalar float):

```csharp
sealed class Textured : ISharpShader
{
    [Group(0), Binding(2)] static Texture2D<float> Color;
    [Group(0), Binding(3)] static SamplerState Linear;

    [Fragment]
    [return: Location(0)]
    public static vec4f32 Shade([Location(0)] vec2f32 uv)
        => Color.SampleLevel(Linear, uv, 0.0f);
}
```

**PROPOSED IR**:

```text
Color  : Texture2D<FilterableF32> binding=(0,2), sampled, single-sample
Linear : Sampler<Ordinary>       binding=(0,3)
Shade  : (location(0):vec2<f32>) -> location(0):vec4<f32>, stage=Fragment
return SampleLevel(Color, Linear, uv:vec2<f32>, lod:f32=0) : vec4<f32>
```

Resource kind supplies opaque handle semantics; it must not be modeled as a
pointer to texels in uniform/storage memory. Reflection must report texture
dimension/sample type/multisampling and sampler kind, plus distinct binding
pairs. The host must supply a compatible filterable format and sampler; C#'s
`float` alone cannot prove device/format filtering capability.

**INTENDED SLANG LOWERING**:

```slang
[[vk::binding(2, 0)]] Texture2D<float4> Color;
[[vk::binding(3, 0)]] SamplerState Linear;
[shader("fragment")]
float4 Shade(float2 uv : TEXCOORD0) : SV_Target0
{
    return Color.SampleLevel(Linear, uv, 0.0f);
}
```

**INTENDED WGSL SEMANTICS**:

```wgsl
@group(0) @binding(2) var color: texture_2d<f32>;
@group(0) @binding(3) var linear: sampler;
@fragment fn Shade(@location(0) uv: vec2<f32>) -> @location(0) vec4<f32> {
    return textureSampleLevel(color, linear, uv, 0.0);
}
```

**FUTURE ACCEPTANCE:** a one-texel RGBA texture `(0.25,0.5,0.75,1)`,
clamp-to-edge ordinary sampler, LOD 0 and uv `(0.5,0.5)` yields that color.
Compiler acceptance checks the resource/operation types and both targets; actual
sampling remains a separate GPU test. Wrong dimension, comparison sampler or
integer texture supplied to this operation must reject before target text.

**SEPARATE PROPOSED IMPLICIT-LOD VARIANT:** C# `Color.Sample(Linear, uv)`
maps to typed `SampleImplicit` and Slang `.Sample` / WGSL `textureSample`.
It requires fragment-stage derivative-valid participation and the target's
uniformity rules. This unconditional entry-body example avoids a divergent
branch; a future negative case is sampling only under `uv.x > 0.5`.
The frontend needs an agreed enforce/reject/diagnostic contract for that case
with #154; current CLSL has none. These are four different claims:

| Boundary | Claim |
|---|---|
| Current frontend | No sampling API or derivative-uniformity checker |
| Future target validation | Record actual Slang/WGSL diagnostics for each case |
| WGSL specification | `textureSample` is fragment-only and subject to `derivative_uniformity`; explicit LOD does not need implicit derivatives |
| GPU execution | No maximal reconvergence, quad participation or helper-invocation guarantee established by these snippets |

Explicit LOD is a smaller first slice, not a fix for arbitrary barriers,
subgroups, derivative calculations used to form arguments, or reconvergence.
Storage/depth/array/multisample textures, comparison sampling, gather, load/store
and explicit gradients are separate follow-ups, not overloads implied here.

## 3. Uniform layout and reflection

**PROPOSED C#** (`ShaderLayout` is a proposed layout contract, not CLR layout):

```csharp
[ShaderLayout(ShaderLayoutKind.PortableUniform)]
struct Params
{
    public vec4f32 Tint;
    public float Exposure;
    public float Gamma;
    public vec2f32 Padding;
}

// In an ISharpShader module:
[Uniform, Group(1), Binding(2)] static Params Settings;

[Fragment]
[return: Location(0)]
public static vec4f32 Shade() => Settings.Tint * Settings.Exposure;
```

**PROPOSED IR / REQUIRED REFLECTION RESULT**, shared by the intended target
layout and explicit host serializer:

```text
Params : struct, align=16, size=32
  Tint     : vec4<f32>, offset=0,  natural-align=16, size=16
  Exposure : f32,       offset=16, natural-align=4,  size=4
  Gamma    : f32,       offset=20, natural-align=4,  size=4
  Padding  : vec2<f32>, offset=24, natural-align=8,  size=8
Settings : Uniform<Params>, access=Read, binding=(group=1,binding=2)
minimumBindingSize=32, dynamicOffset=false
Shade -> LoadMember(Settings,Tint)*LoadMember(Settings,Exposure)
```

No implicit tail padding is left in this example. Array stride and matrix
orientation are **not applicable**: this first profile rejects arrays, matrices,
nested structs and bool rather than guessing their ABI. WGSL bool is not
host-shareable, even though the specification gives bool a size/alignment.
Later profiles must define array stride and row/column orientation explicitly.
Natural member alignment above is not a required textual `@align` spelling:
target lowering may strengthen alignment while preserving the exact offsets,
struct alignment and size. The captured Slang WGSL does so for `Exposure`.

**INTENDED SLANG LOWERING**:

```slang
struct Params
{
    float4 Tint;
    float Exposure;
    float Gamma;
    float2 Padding;
}
[[vk::binding(2, 1)]] ConstantBuffer<Params> Settings;
[shader("fragment")]
float4 Shade() : SV_Target0
{
    return Settings.Tint * Settings.Exposure;
}
```

**INTENDED WGSL SEMANTICS**:

```wgsl
struct Params {
    Tint: vec4<f32>,
    Exposure: f32,
    Gamma: f32,
    Padding: vec2<f32>,
}
@group(1) @binding(2) var<uniform> settings: Params;
@fragment fn Shade() -> @location(0) vec4<f32> {
    return settings.Tint * settings.Exposure;
}
```

**FUTURE ACCEPTANCE:** explicit 32-byte little-endian IEEE-f32 serialization
for Tint `(1,0.5,0.25,1)`, Exposure `2`, Gamma `1`, Padding `(0,0)`:

```text
0000803f 0000003f 0000803e 0000803f
00000040 0000803f 00000000 00000000
```

The shader result is `(2,1,0.5,2)`. Assert offsets/size/binding from the applied
Slang target layout and compare the host byte fixture; a mismatch is a diagnostic,
not success with a guessed `Marshal.SizeOf`. Do not upload generated CLR vector
structs by assuming their backing SIMD layout matches this contract. The
`PortableUniform` policy must either prove this layout on each supported target
or reject that target; it cannot promise every target shares Slang's chosen ABI.
Device uniform-buffer offset alignment is separate from the 16-byte struct
alignment. Host buffer allocation/binding and runtime result are follow-ups.

## Upstream provenance and limits

Examples above are original minimal designs; no upstream suites/source examples
were imported. All three hand-authored Slang snippets were separately compiled
to WGSL by the pinned Slang; the uniform reference also produced offsets
0/16/20/24 and a 32-byte element layout. That is **target-only evidence**, not
evidence that the proposed C# API or IR exists. See
[captured reference outputs](./shader-feature-evidence.md#hand-authored-target-reference-probes).
The following immutable references informed the contracts.

| Source | Pinned revision and paths | License |
|---|---|---|
| WGSL normative specification | gpuweb/gpuweb `358eebc8e7bf2d6efa41a4b8b3fbc3a715288204`, [wgsl/index.bs][wgsl]: host-shareable types (3704-3722), resource interface (11401-11489), layout (11728-12320), uniformity (12504-12555), sampling (18947-19024,19594-19793) | [LICENSE.md][wgsl-license]: documents under the W3C Software and Document License (2023); repository software `BSD-3-Clause` |
| Slang compiler/docs | shader-slang/slang `69947dec841ea46e68ccdccae45a1080fcaea01c` (`v2025.12.1`), [conventional features][slang-types] (292-348,610-634), [SPIR-V target][slang-spirv] (184-208,244-248,426-436), [layout][slang-layout], [reflection][slang-reflect] (322-450,509-519) | [LICENSE][slang-license]: `Apache-2.0 WITH LLVM-exception` |
| ILGPU managed compute design | m4rs-mt/ILGPU `ea51bcbdc3695554b8b9899a225d37c12cd0babe`, [memory views][ilgpu-views] and [kernels][ilgpu-kernels] | [LICENSE.txt][ilgpu-license]: `NCSA` |

Repository `flake.lock` pins nixpkgs `5fa1ef6f8d1a66829571930adfa6e214caaff9a6`;
its [shader-slang package][nix-slang] selects the Slang version cited here.
WGSL reference revision is a research pin, **not** a claim the pinned Slang
implements every newer WGSL extension.

Slang documents target-dependent layout and Vulkan's binding-first/set-second
annotation. D3D has distinct b/t/s/u namespaces; direct Vulkan mapping cannot
reuse the same pair for texture and sampler. Require an explicit mapping policy
before supporting another target, not a backend string parameter.

ILGPU `ArrayView` is useful precedent for typed index/length compute APIs;
host `MemoryBuffer` is not a kernel parameter. Its debug view assertions and
implicit grouping do not establish bounds safety or fixed workgroup semantics
for CLSL. ILGPU is not a graphics texture/sampler/fragment ABI reference.

[wgsl]: https://github.com/gpuweb/gpuweb/blob/358eebc8e7bf2d6efa41a4b8b3fbc3a715288204/wgsl/index.bs
[wgsl-license]: https://github.com/gpuweb/gpuweb/blob/358eebc8e7bf2d6efa41a4b8b3fbc3a715288204/LICENSE.md
[slang-types]: https://github.com/shader-slang/slang/blob/69947dec841ea46e68ccdccae45a1080fcaea01c/docs/user-guide/02-conventional-features.md
[slang-spirv]: https://github.com/shader-slang/slang/blob/69947dec841ea46e68ccdccae45a1080fcaea01c/docs/user-guide/a2-01-spirv-target-specific.md
[slang-layout]: https://github.com/shader-slang/slang/blob/69947dec841ea46e68ccdccae45a1080fcaea01c/docs/layout.md
[slang-reflect]: https://github.com/shader-slang/slang/blob/69947dec841ea46e68ccdccae45a1080fcaea01c/docs/user-guide/09-reflection.md
[slang-license]: https://github.com/shader-slang/slang/blob/69947dec841ea46e68ccdccae45a1080fcaea01c/LICENSE
[ilgpu-views]: https://github.com/m4rs-mt/ILGPU/blob/ea51bcbdc3695554b8b9899a225d37c12cd0babe/Docs/03_Advanced/01_Memory-Buffers-and-Views.md
[ilgpu-kernels]: https://github.com/m4rs-mt/ILGPU/blob/ea51bcbdc3695554b8b9899a225d37c12cd0babe/Docs/03_Advanced/02_Kernels.md
[ilgpu-license]: https://github.com/m4rs-mt/ILGPU/blob/ea51bcbdc3695554b8b9899a225d37c12cd0babe/LICENSE.txt
[nix-slang]: https://github.com/NixOS/nixpkgs/blob/5fa1ef6f8d1a66829571930adfa6e214caaff9a6/pkgs/by-name/sh/shader-slang/package.nix

# Texture2D explicit-LOD sampling

CLSL supports one sampled-texture profile: a non-arrayed, single-sampled,
filterable-f32 2D texture paired with an ordinary non-comparison sampler.

```csharp
using DualDrill.CLSL;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.Mathematics;

sealed class TextureShader : ISharpShader
{
    [Group(0), Binding(2)]
    private static readonly Texture2D<float> Color;

    [Group(0), Binding(3)]
    private static readonly SamplerState Linear;

    [Fragment]
    [return: Location(0)]
    public static vec4f32 Shade([Location(0)] vec2f32 uv) =>
        Color.SampleLevel(Linear, uv, 0.0f);
}
```

`Texture2D<T>` and `SamplerState` are zero-field readonly shader handles. Only
the exact closed `Texture2D<float>` type is mapped. `SampleLevel` takes
`in SamplerState`, preserving the ordinary call syntax while keeping both
resources as addressed bindings in CIL. Its result is `vec4f32`.

The compiler lowers this call to the exact typed operation:

```text
ptr<Handle, Texture2D<filterable-f32>>,
ptr<Handle, Sampler<ordinary>>,
vec2<f32>,
f32
    -> vec4<f32>
```

The operation reports `MemoryRead`, not `DerivativeQuad`, and is valid in
vertex, fragment, and compute code. Requirements of expressions used to produce
the UV or LOD remain independent.

Slang uses `Texture2D<vec4<f32>>`, `SamplerState`, and `.SampleLevel`; the WGSL
target uses `texture_2d<f32>`, `sampler`, and `textureSampleLevel`.

## Reflection and descriptors

`GetTextureBindings` reports name, group, binding, OR-combined visibility,
texture kind, 2D dimension, float sample type, and `Multisampled=false`.
`GetSamplerBindings` reports the same identity and visibility plus a filtering,
non-comparison sampler contract. `Filtering` describes the bind-group layout;
it does not prove that a host sampler uses linear filters.

`GetBindGroupLayoutDescriptor(module, group)` combines uniform, read-only
storage, writable storage, texture, and sampler entries. Each entry populates
exactly one WebGPU layout arm.

`GetBindGroupLayoutDescriptorBuffer(module, group)` remains usable for
buffer-only groups. It rejects a selected group containing texture or sampler
bindings and directs callers to the general descriptor instead of silently
dropping handles.

Texture and sampler bindings do not support dynamic offsets. Each requires
exactly one nonnegative `[Group]` and `[Binding]`, and coordinates remain unique
across every resource kind.

## Boundaries

The profile does not add implicit-LOD sampling, comparison sampling, storage or
depth textures, integer textures, arrays, cube/3D textures, or multisampling.
Resource handles are static module fields only; locals, parameters, returns,
nested members, properties, copies, and conflicting `[Uniform]`, `[Read]`, or
`[ReadWrite]` metadata reject before target emission.

A one-texel RGBA value `(0.25, 0.5, 0.75, 1)`, sampled at UV `(0.5, 0.5)` and
LOD `0`, is only a source-level reference expectation. The compiler does not
prove host texture format/filterability, sampler configuration, mip or
coordinate validity, device limits, GPU execution, or readback.

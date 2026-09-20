# Uniform buffer layout

CLSL supports a bounded WGSL-via-Slang uniform layout profile. Validation runs
in `SlangTargetLowering.Lower`, before a Slang target AST is produced, whether
lowering is reached through `CLSLCompiler.Emit` or called directly.
Resource identity and annotation rules are defined in
[Shader metadata validation](./shader-metadata.md).

## Authoring

```csharp
using DualDrill.CLSL;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Reflection;
using DualDrill.Mathematics;
using static DualDrill.Mathematics.DMath;

sealed class LayoutShader : ISharpShader
{
    public struct Params
    {
        public vec4f32 Tint;
        public float Exposure;
        public uint Mode;
        public vec2f32 Padding;
    }

    [Group(1), Binding(2), Uniform]
    private static readonly Params Settings;

    [Fragment]
    [return: Location(0)]
    public static vec4f32 Shade() =>
        Settings.Tint * Settings.Exposure
        + vec4(Settings.Padding, Settings.Padding);
}
```

Supported uniform values are `f32`, `i32`, and `u32`, their rank-2/3/4 vectors,
and nonempty structures whose instance members are fields of those types.
Declaration order is preserved.

## Layout

Direct scalar and vector uniforms retain their own alignment and size:

| Type | Alignment | Size |
|---|---:|---:|
| `f32`, `i32`, `u32` | 4 | 4 |
| rank-2 vector | 8 | 8 |
| rank-3 vector | 16 | 12 |
| rank-4 vector | 16 | 16 |

A structure has alignment 16 and its final size is padded to a multiple of 16.
For each member, Slang strengthens the effective alignment to the largest
power of two, capped at 16, that divides the member offset. The natural
alignment remains the lower bound. Thus offsets 0, 4, 8, 12, and 16 have
effective alignment ceilings 16, 4, 8, 4, and 16 respectively.

For `Params` above:

| Member | Offset | Natural alignment | Effective alignment | Size |
|---|---:|---:|---:|---:|
| `Tint` | 0 | 16 | 16 | 16 |
| `Exposure` | 16 | 4 | 16 | 4 |
| `Mode` | 20 | 4 | 4 | 4 |
| `Padding` | 24 | 8 | 8 | 8 |

The structure alignment and size are 16 and 32. A one-scalar structure is also
16 bytes; a direct scalar uniform remains 4 bytes.

An illustrative little-endian 32-byte value with
`Tint=(1,2,3,4)`, `Exposure=0.5`, `Mode=1u`, and `Padding=(5,6)` is:

```text
0000803f0000004000004040000080400000003f010000000000a0400000c040
```

In particular, `Mode=1u` at offset 20 is `01000000`, not the floating-point
encoding `0000803f`. This is a byte-level fixture for the shader contract, not
a claim about CLR `sizeof`, automatic upload, or CPU structure layout.

## Reflection and descriptors

```csharp
var compiler = new CLSLCompiler(new(CLSLCompileTarget.SLang));
var module = compiler.Parse(new LayoutShader());
IShaderModuleReflection reflection = new ShaderModuleReflection();

var uniforms = reflection.GetUniformBindings(module);
var group1 = reflection.GetBindGroupLayoutDescriptor(module, 1);
var group1Buffers = reflection.GetBindGroupLayoutDescriptorBuffer(module, 1);
```

`GetUniformBindings` returns each uniform's exact name, group, binding,
`GPUBufferBindingType.Uniform` kind, OR-combined stage visibility,
dynamic-offset flag, total layout, and member offsets/sizes/natural/effective
alignments. It first applies the shared module metadata validator, so missing or
negative coordinates, duplicate binding pairs, and address-space mismatches
fail with the same diagnostics as compilation.

Both descriptor methods require an explicit nonnegative group. An absent group
returns an empty descriptor. Their entries are projections of
`GetUniformBindings`, and `MinBindingSize` is the reflected layout size.
The legacy controller routes likewise require the group:

```text
GET /ILSL/wgsl/bindgrouplayoutdescriptor/{name}/{group}
GET /ILSL/wgsl/bindgrouplayoutdescriptorbuffer/{name}/{group}
```

`ShaderBufferLayout.Alignment` and member alignment are shader ABI facts.
Dynamic buffer offsets must separately satisfy the selected device's
`minUniformBufferOffsetAlignment`; this compiler does not query or prove that
runtime constraint.

## Rejected shapes and evidence boundary

Before target AST/text production, CLSL rejects uniform bools, narrow/wide
scalars and vectors, arrays, matrices and opaque handles, nested or empty
structures, property-bearing structures, and explicit CLR or `[Align]` layout.

Tests exercise C# parsing, typed IR, public Slang/WGSL emission, Slang reflection
JSON, and descriptor projection. They do not perform CPU upload, GPU dispatch,
rendering, or readback.

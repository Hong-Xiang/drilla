# Shader feature evidence

Snapshot: 2026-09-20. Compiler source baseline:
`fb7304a30b9102ac770ba7547512dd9bdf65c9d3`; characterization tests are part of
this documentation/evidence slice. No production compiler files were changed.
The [inventory](./shader-feature-inventory.md) distinguishes source inspection,
current CLSL behavior and [future API proposals](./shader-feature-proposals.md).

> **Historical snapshot.** The captures below remain attributed to the stated
> baseline. For current compute-signature support, see
> [Compute entry points](./compute-entry.md); for current storage support, see
> [Read-only f32 structured buffers](./readonly-structured-buffer.md) and
> [Writable f32 structured buffers](./writable-structured-buffer.md).

## Current CLSL probes

The [new test file](../../DualDrill.CLSL.Test/ShaderFeatureCharacterizationTests.cs)
contains five gap characterizations. The selected command in the
[inventory](./shader-feature-inventory.md#runnable-evidence) runs them together
with three existing public compiler positives: minimum triangle, uniform
struct and boolean helper calls. Actual Debug and Release output each included:

```text
Test Run Successful.
Total tests: 8
     Passed: 8
```

These are real C# test-assembly fixtures in both CIL configurations, not a
`Compiler.Server` acceptance run, hand-built shared IR, or GPU execution.
The first `--no-restore` attempt failed with `NETSDK1004` (missing assets);
`dotnet restore DualDrill.CLSL.Test/DualDrill.CLSL.Test.csproj` was run only
afterward. Existing duplicate-package and ImageSharp advisory warnings remain;
no dependency manifest was changed.

| Actual input | Actual stopping/output boundary | Meaning |
|---|---|---|
| `[Compute] public static void cs() { }` | Public Slang compile reaches emitter, then exact `NotSupportedException` below | Stage discovery works; compute emission does not |
| Vertex `vec4f32 vs([Builtin(instance_index)] uint index)` returning a constant vector | Public Slang compile reaches builtin emission, then exact rejection | Enum declaration does not imply builtin support |
| `static int ReadElement(int[] values, int index) => values[index]` | `int[]` maps to `OpaqueType`; real CIL `ldelem.i4` fails Pre analysis with `ValidationException`, inner `NotImplementedException` | No usable array/index path |
| `texture.Sample(sampler, uv)` using legacy `ITexture2D<Vector4>`/`ISampler` | Both resource types map to `OpaqueType`; metadata collection fails at `callvirt`, before shader operations | Legacy interface is not a shader intrinsic |
| VertexOut pass-through with annotated Position/Uv fields | Parsed `StructureDeclaration` retains both attributes; public Slang output has no member semantics | Accepted-but-erased metadata, not target-valid interface evidence |

**ACTUAL DIAGNOSTICS**, identical for Debug and Release:

```text
Slang attribute DualDrill.CLSL.Language.ShaderAttribute.ComputeAttribute is not supported.
Unsupported Slang builtin binding instance_index.
reachable instruction semantics are not supported at IL_0002 (ldelem.i4). @ ReadElement
Failed to collect metadata operand at IL_0003 (callvirt) in System.Numerics.Vector4 Sample(DualDrill.CLSL.ITexture2D`1[System.Numerics.Vector4], DualDrill.CLSL.ISampler, System.Numerics.Vector2).
Referenced method System.Numerics.Vector4 Sample(DualDrill.CLSL.ISampler, System.Numerics.Vector2) has no decodable CIL body and is not a registered builtin or intrinsic.
```

**ACTUAL C# INPUT** for the metadata-loss probe:

```csharp
private struct VertexOut
{
    [Builtin(BuiltinBinding.position)] public vec4f32 Position;
    [Location(0)] public vec2f32 Uv;
}
// In the ISharpShader fixture:
[Vertex] public static VertexOut vs(VertexOut value) => value;
```

**ACTUAL EMITTED SLANG STRUCT**, not proposed syntax:

```slang
struct VertexOut{
    vec4<f32>Position;
    vec2<f32>Uv;
}
```

The test checks both collected attributes and these exact member lines, plus
absence of `SV_POSITION` and `TEXCOORD0` from output. It does not quietly pass
because an annotated member was never collected.

Probe calibration also found that converting the instance index from uint to
float in the body fails earlier with
`reachable instruction semantics are not supported at IL_0001 (conv.r.un). @ vs`.
The final builtin probe deliberately leaves that input unused to isolate builtin
emission. This is an additional observed scalar gap, not a fix or a claim that
all unsigned-to-float source forms reach the same opcode.

The public positive uniform fixture and its asserted WGSL/reflection output
are described in the inventory. No test is skipped, marked expected-failure,
or made permissive to accept any diagnostic.

## Hand-authored target reference probes

**Actual target-only captures, not C#-compiled output.** Each `slang` fence in
the proposal document was extracted verbatim and passed to pinned
`slangc v2025.12.1-nixpkgs`. These establish that the proposed target subset has
a viable Slang-to-WGSL path; they do not implement or approve its C# or shared-IR
surface, execute WGSL, or prove GPU participation semantics.

Reproduction from the repository root (the temporary directory holds outputs):

```sh
nix develop --builders '' --command bash -c '
set -euo pipefail
out="$(mktemp -d)"
for n in 1 2 3; do
    awk -v n="$n" '"'"'
        /^```slang$/ { block++; active=1; next }
        /^```$/ { active=0 }
        active && block==n { print }
    '"'"' docs/clsl/shader-feature-proposals.md > "$out/reference-$n.slang"
    slangc "$out/reference-$n.slang" -target wgsl \
        -o "$out/reference-$n.wgsl" -reflection-json "$out/reference-$n.json"
    printf "Reference %s: Slang -> WGSL + reflection succeeded\n" "$n"
done
printf "Outputs: %s\n" "$out"
'
```

Actual successful output, with outputs retained in the lead's evidence artifact
directory instead of `mktemp` during capture:

```text
Reference 1: Slang -> WGSL + reflection succeeded
Reference 2: Slang -> WGSL + reflection succeeded
Reference 3: Slang -> WGSL + reflection succeeded
```

Reference 1 (guarded f32 storage buffers): actual WGSL:

```wgsl
@binding(0) @group(0) var<storage, read> Input_0 : array<f32>;

@binding(1) @group(0) var<storage, read_write> Output_0 : array<f32>;

@compute
@workgroup_size(64, 1, 1)
fn Run(@builtin(global_invocation_id) id_0 : vec3<u32>)
{
    var _S1 : vec2<u32> = vec2<u32>(arrayLength(&Input_0), 4);
    var inputCount_0 : u32 = _S1.x;
    var _S2 : vec2<u32> = vec2<u32>(arrayLength(&Output_0), 4);
    var outputCount_0 : u32 = _S2.x;
    var _S3 : u32 = id_0.x;
    var _S4 : bool;
    if(_S3 < inputCount_0)
    {
        _S4 = _S3 < outputCount_0;
    }
    else
    {
        _S4 = false;
    }
    if(_S4)
    {
        Output_0[_S3] = Input_0[_S3] * 2.0f;
    }
    return;
}
```

Reference 2 (explicit-LOD fragment sample): actual WGSL contains:

```wgsl
@binding(2) @group(0) var Color_0 : texture_2d<f32>;

@binding(3) @group(0) var Linear_0 : sampler;
```

The emitted fragment returns a location-0 wrapper containing
`textureSampleLevel((Color_0), (Linear_0), (_S1.uv_0), (0.0f))`.
There was no implicit-LOD or divergent-participation execution in this probe.

Reference 3 (uniform): actual WGSL declaration:

```wgsl
struct Params_std140_0
{
    @align(16) Tint_0 : vec4<f32>,
    @align(16) Exposure_0 : f32,
    @align(4) Gamma_0 : f32,
    @align(8) Padding_0 : vec2<f32>,
};

@binding(2) @group(1) var<uniform> Settings_0 : Params_std140_0;
```

Actual reflection JSON at `parameters[0]` reports
`binding={"kind":"descriptorTableSlot","space":1,"index":2}`,
`type.kind="constantBuffer"`, and
`type.elementVarLayout.binding={"kind":"uniform","offset":0,"size":32}`.
The `type.elementType.fields[*].binding` entries are:

| Field | Actual `kind` | Actual offset | Actual size |
|---|---|---|---|
| Tint | uniform | 0 | 16 |
| Exposure | uniform | 16 | 4 |
| Gamma | uniform | 20 | 4 |
| Padding | uniform | 24 | 8 |

The JSON capture does not provide an alignment field here. Struct alignment 16
is derived from the emitted WGSL and WGSL layout rules, **not** a captured JSON
alignment property. Stronger emitted alignment for `Exposure` preserves offset
16 and total size 32. There was no host upload or pixel readback.

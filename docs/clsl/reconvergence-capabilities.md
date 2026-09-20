# Reconvergence target capability evidence

Observed 2026-09-20 for the [proposed contract](./reconvergence-contract.md).
**No GPU execution, browser run, device-feature query or runtime guarantee.**
These standalone Slang probes bypass CLSL; they do not demonstrate preservation
of the contract through the CIL/Region/target-lowering pipeline.

## Exact versions

The repository's nixpkgs pin is
`5fa1ef6f8d1a66829571930adfa6e214caaff9a6`. Its [package expression][nix-slang]
selects **Slang 2025.12.1**, not the 2025.23.2 mentioned as unverified in the older
research snapshot. `slangc -version` actually printed `v2025.12.1-nixpkgs`.
Upstream release commit: `69947dec841ea46e68ccdccae45a1080fcaea01c`.
The local derivation was
`/nix/store/ncv5r55rvil1qnf350jd0z47xmcnpk8w-shader-slang-2025.12.1.drv`.

Additional validators came from the **same nixpkgs commit**, after the dev-shell
`spirv-val` command failed with exit 127. Observed versions:
`SPIRV-Tools v2025.2 unknown hash` and `Glslang Version: 11:15.3.0`.
No repository pin, dependency manifest, native provider or Slang version changed.

The separately inspected upstream Slang snapshot is
`282587ac1c04ad8cbd6592e6e12142a35b2687e8`; source inspection of it is not a local
execution result.

## Primary specifications and implementation

- [SPV_KHR_maximal_reconvergence][spirv] is revision 2, last modified 2024-04-18.
  It introduces an execution mode, not a new `OpCapability`. Its [related dynamic
  instances and non-reconvergence rules][relations] distinguish selection merge,
  loop entry, iteration continue, case entry and call return. The annotation's
  [static call tree restrictions][structure] still apply; ordinary scalar trace
  preservation is not a correspondence proof.
- [Switch divergence rules][switch] keep equal selector values together but
  permit different values selecting a common target to diverge. The decision is
  deterministic for a compilation. Shared target, shared default arm and shared
  tangle are therefore different concepts.
- GPUWeb snapshot `358eebc8e7bf2d6efa41a4b8b3fbc3a715288204` has no maximal-
  reconvergence entry in its [WebGPU feature list][features]; [shader creation][module]
  takes WGSL, not application-supplied SPIR-V. Internal browser Vulkan use does
  not let a WebGPU application request this execution mode.
- [WGSL active-invocation rules][active] warn that divergent subgroup operations
  may have different active sets than expected. [Subgroup uniformity][uniformity]
  is static analysis at a scope, not maximal reconvergence. [Barriers][barriers]
  and [derivatives][derivatives] retain their separate control-flow requirements.
- Pinned Slang [declares the attribute][attribute], emits [SPIR-V markers][emitter]
  and also emits the [GLSL extension/attribute][glsl-emitter]. The declaration's
  comment that other targets are unaffected is incomplete for GLSL.
- Pinned [`WaveActiveCountBits` requirements][wave] exclude WGSL. The
  [upstream declaration][upstream-wave] also excludes WGSL; this is a Slang
  builtin limitation, not a statement that WGSL has no subgroup operations.
  Current upstream retains [SPIR-V][upstream-spirv] and [GLSL][upstream-glsl]
  attribute emission.

## Inputs and observed results

The committed [wave input](./examples/reconvergence/annotated-wave.slang) has
`WaveActiveCountBits(true)` in both divergent arms; the odd arm adds 100 so the
arms are not trivially identical. The [scalar control](./examples/reconvergence/annotated-scalar.slang)
has the same branch shape without subgroup work. Each baseline is the same file
with only `[MaximallyReconverges]` removed.

Workgroup size 4 is an input, **not an assumption of subgroup size 4**. No output
buffer was read, and no expected hardware lane-count result is asserted.

| Target/check | Wave baseline / annotated exit | Scalar baseline / annotated exit | Evidence |
|---|---:|---:|---|
| Slang -> WGSL | 255 / 255 | 0 / 0 | Compile-only |
| Slang -> SPIR-V assembly and binary | 0 / 0 | 0 / 0 | Compile-only |
| SPIRV-Tools universal and Vulkan 1.1 | 0 / 0 | 0 / 0 | Validate-only |
| Slang -> GLSL | 0 / 0 | 0 / 0 | Compile-only |
| glslang `-V` default | 2 / 2 | 0 / 0 | Validation attempt |
| glslang `-V --target-env vulkan1.1` | 0 / 0 | 0 / 0 | Validate/translate-only |
| Slang -> HLSL or CUDA source (peripheral) | 0 / 0 | 0 / 0 | Compile-only, not product support |

Captured annotated-wave WGSL diagnostic:

```text
annotated-wave.slang(6): error 36107: entrypoint 'main' uses features that are not available in 'compute' stage for 'wgsl' target.
void main(uint3 dispatchThreadID : SV_DispatchThreadID)
     ^~~~
annotated-wave.slang(12): note: see using of 'WaveActiveCountBits'
        activeCount = WaveActiveCountBits(true);
                      ^~~~~~~~~~~~~~~~~~~
hlsl.meta.slang(15395): note: see definition of 'WaveActiveCountBits'
hlsl.meta.slang(15394): note: see declaration of 'require'
```

Both scalar WGSL outputs were byte-identical, with SHA-256
`7f01b465240c731d4b421182e8c288c0a9dd9665f2e8a4479f8b1dbbbd9b3654`.
**Accepted and ignored is not a successful reconvergence implementation.**
Captured output:

```wgsl
@binding(0) @group(0) var<storage, read_write> outputBuffer_0 : array<u32>;

@compute
@workgroup_size(4, 1, 1)
fn main(@builtin(global_invocation_id) dispatchThreadID_0 : vec3<u32>)
{
    var _S1 : u32 = dispatchThreadID_0.x;
    var value_0 : u32;
    if(((_S1 & (u32(1)))) == u32(0))
    {
        value_0 = u32(1);
    }
    else
    {
        value_0 = u32(101);
    }
    outputBuffer_0[_S1] = value_0;
    return;
}
```

Captured annotated SPIR-V adds:

```text
OpExtension "SPV_KHR_maximal_reconvergence"
OpExecutionMode %main MaximallyReconvergesKHR
```

Both wave modules contain `OpSelectionMerge` and two
`OpGroupNonUniformBallot` / `OpGroupNonUniformBallotBitCount` sequences. All four
binaries validate in universal and Vulkan 1.1 environments. This establishes
validity of these tiny modules, not general CLSL region correspondence.

Captured annotated GLSL adds:

```glsl
#extension GL_EXT_maximal_reconvergence : require
[[maximally_reconverges]]
```

The [GLSL extension text][glsl-extension] is draft and maps the attribute to the
SPIR-V mode. Pinned glslang accepts it; disassembly of the annotated translated
module contains that mode. The first wave attempt's default `-V` environment
failed because subgroup operations require SPIR-V 1.3; explicit Vulkan 1.1
succeeded. Do not erase this target-version distinction.

For the HLSL/CUDA source controls, removing source-line and blank-line differences
left identical annotated and baseline output. That observation supplies no
target-independent reconvergence guarantee. Neither downstream toolchain ran.

## Reproducing the principal probes

From the repository root, use a fresh output directory and derive the baselines
without changing the committed inputs:

```sh
probe="$(mktemp -d)"
for kind in wave scalar; do
  cp "docs/clsl/examples/reconvergence/annotated-$kind.slang" "$probe/"
  sed '/^\[MaximallyReconverges\]$/d' \
    "$probe/annotated-$kind.slang" > "$probe/baseline-$kind.slang"
done
for kind in wave scalar; do
  for mode in baseline annotated; do
    input="$probe/$mode-$kind.slang"
    nix develop --builders '' --command slangc "$input" \
      -entry main -stage compute -target spirv -profile sm_6_0 \
      -o "$probe/$mode-$kind.spv"
    nix develop --builders '' --command slangc "$input" \
      -entry main -stage compute -target spirv-asm -profile sm_6_0 \
      -o "$probe/$mode-$kind.spvasm"
    nix develop --builders '' --command slangc "$input" \
      -entry main -stage compute -target wgsl -profile sm_6_0 \
      -o "$probe/$mode-$kind.wgsl"
    printf 'WGSL %s-%s exit=%s\n' "$mode" "$kind" "$?"
  done
done
cmp "$probe/baseline-scalar.wgsl" "$probe/annotated-scalar.wgsl"
```

The two wave WGSL failures above are expected; run the block without shell
`errexit` so both scalar controls also run. Inspect all command exits rather than
treating the final `cmp` as the result of the whole experiment.

If `spirv-val` is absent, record that failure before fetching the same-pin tool:

```sh
nix shell --builders '' \
  github:NixOS/nixpkgs/5fa1ef6f8d1a66829571930adfa6e214caaff9a6#spirv-tools \
  --command spirv-val --target-env vulkan1.1 "$probe/annotated-wave.spv"
```

For each input, the GLSL command substitutes `-target glsl -profile glsl_460`.
Use same-pin `#glslang` with
`glslangValidator -V --target-env vulkan1.1 -S comp input.glsl -o output.spv`.
These commands compile and validate; none dispatches work.

## Limit of the evidence

An actual Vulkan path would additionally need device support and enablement of
`VK_KHR_shader_maximal_reconvergence` and
`VkPhysicalDeviceShaderMaximalReconvergenceFeaturesKHR.shaderMaximalReconvergence`,
plus correct final entry-point call-tree structure. None is queried or enabled
here. There is no optional Vulkan runtime profile in this PR.

[nix-slang]: https://github.com/NixOS/nixpkgs/blob/5fa1ef6f8d1a66829571930adfa6e214caaff9a6/pkgs/by-name/sh/shader-slang/package.nix#L28-L37
[spirv]: https://github.com/KhronosGroup/SPIRV-Registry/blob/5614bd850c89e15efc7bc85a65c613019b732867/extensions/KHR/SPV_KHR_maximal_reconvergence.asciidoc
[relations]: https://github.com/KhronosGroup/SPIRV-Registry/blob/5614bd850c89e15efc7bc85a65c613019b732867/extensions/KHR/SPV_KHR_maximal_reconvergence.asciidoc#L212-L308
[structure]: https://github.com/KhronosGroup/SPIRV-Registry/blob/5614bd850c89e15efc7bc85a65c613019b732867/extensions/KHR/SPV_KHR_maximal_reconvergence.asciidoc#L99-L115
[switch]: https://github.com/KhronosGroup/SPIRV-Registry/blob/5614bd850c89e15efc7bc85a65c613019b732867/extensions/KHR/SPV_KHR_maximal_reconvergence.asciidoc#L185-L210
[features]: https://github.com/gpuweb/gpuweb/blob/358eebc8e7bf2d6efa41a4b8b3fbc3a715288204/spec/index.bs#L1544-L1575
[module]: https://github.com/gpuweb/gpuweb/blob/358eebc8e7bf2d6efa41a4b8b3fbc3a715288204/spec/index.bs#L7185-L7199
[active]: https://github.com/gpuweb/gpuweb/blob/358eebc8e7bf2d6efa41a4b8b3fbc3a715288204/wgsl/index.bs#L14141-L14159
[uniformity]: https://github.com/gpuweb/gpuweb/blob/358eebc8e7bf2d6efa41a4b8b3fbc3a715288204/wgsl/index.bs#L2011-L2024
[barriers]: https://github.com/gpuweb/gpuweb/blob/358eebc8e7bf2d6efa41a4b8b3fbc3a715288204/wgsl/index.bs#L14163-L14170
[derivatives]: https://github.com/gpuweb/gpuweb/blob/358eebc8e7bf2d6efa41a4b8b3fbc3a715288204/wgsl/index.bs#L14200-L14208
[attribute]: https://github.com/shader-slang/slang/blob/69947dec841ea46e68ccdccae45a1080fcaea01c/source/slang/core.meta.slang#L4262-L4265
[emitter]: https://github.com/shader-slang/slang/blob/69947dec841ea46e68ccdccae45a1080fcaea01c/source/slang/slang-emit-spirv.cpp#L5249-L5252
[glsl-emitter]: https://github.com/shader-slang/slang/blob/69947dec841ea46e68ccdccae45a1080fcaea01c/source/slang/slang-emit-glsl.cpp#L1533-L1540
[wave]: https://github.com/shader-slang/slang/blob/69947dec841ea46e68ccdccae45a1080fcaea01c/source/slang/hlsl.meta.slang#L15394-L15406
[upstream-wave]: https://github.com/shader-slang/slang/blob/282587ac1c04ad8cbd6592e6e12142a35b2687e8/source/slang/hlsl.meta.slang#L17474-L17486
[upstream-spirv]: https://github.com/shader-slang/slang/blob/282587ac1c04ad8cbd6592e6e12142a35b2687e8/source/slang/slang-emit-spirv.cpp#L6645-L6648
[upstream-glsl]: https://github.com/shader-slang/slang/blob/282587ac1c04ad8cbd6592e6e12142a35b2687e8/source/slang/slang-emit-glsl.cpp#L1564-L1570
[glsl-extension]: https://github.com/KhronosGroup/GLSL/blob/24a73ece80ba5b7e72d73a386470c8f3f7991d67/extensions/ext/GL_EXT_maximal_reconvergence.txt#L17-L67

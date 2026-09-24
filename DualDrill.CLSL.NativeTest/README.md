# Native wgpu smoke test

This optional Linux x64 xUnit project compiles the existing minimum triangle
fixture through the public CLSL WGSL compiler, renders it with native wgpu to a
64×64 offscreen texture, and verifies two readback pixels. It needs no display,
surface, window, or browser.

It also renders input-selected scalar/vector early-return paths, creates a
native shader module from the canonical `DualDrill.Shaders` raymarch assembly, and
verifies that invalid WGSL reports a managed diagnostic instead of throwing
across the native callback boundary. The smoke coverage remains a narrow
baseline; the separate oracle below checks canonical raymarch image parity.

Run it from the repository root with the pinned compiler shell and native wgpu
dependencies available. On the verified Ubuntu/NVIDIA host, nixGL intentionally
and non-hermetically exposes the existing proprietary Vulkan driver:

```sh
NIXPKGS_ALLOW_UNFREE=1 nix develop --builders '' --command \
  nix run --builders '' --impure \
  github:nix-community/nixGL/b6105297e6f0cd041670c3e8628394d4ee247ed5#nixVulkanNvidia -- \
  timeout 120s dotnet test \
  DualDrill.CLSL.NativeTest/DualDrill.CLSL.NativeTest.csproj \
  -c Release -r linux-x64 --logger 'console;verbosity=minimal'
```

The wrapper installs no ICD and forces no software fallback.

The production backend uses the matched `Alimer.Bindings.WebGPU 1.6.0` and
`Alimer.WebGPU.Native 1.0.4` packages. The native package reports
wgpu-native `27.0.4.0`; the tests reject a different loaded ABI before creating
an instance.

The public `DualDrill.Graphics` API remains provider-neutral. Legacy native
extension fields use local `GPU*` enum types, while the backend maps public
enums to Alimer enums by semantic member name and rejects unknown values or
flag bits. `GPUAdapterInfo` reports typed backend and adapter classifications;
callers do not need to infer hardware from vendor or device strings.

Synchronous compute pipelines support automatic or explicit layouts and the
native compute-pass encode/bind/dispatch path. Pipeline constants, compute
timestamp writes, and dynamic bind-group offsets are not supported. A compute
pass must be ended before its parent command encoder is finished; disposing an
unended pass abandons that encoder. GPU resource use and disposal must be
sequential: do not dispose a device or participating resource concurrently
with an operation using it. Concurrent coordination is outside this contract.
Native `Finish` consumes the command encoder even when it reports a validation
error; create a new encoder rather than retrying.

Buffer mapping retains the explicit `IGPUDevice.Poll()` contract. Cancellation
claims only a still-pending map, asks native wgpu to abort it with `Unmap`, and
completes the managed task only after the terminal native callback. If success
wins first, later token cancellation does not unmap the range; the caller owns
the normal `Unmap` in a `finally` block. The wgpu-native 27
`wgpuBufferGetMapState` export is an unimplemented panic stub and is not used.
Queue-work cancellation is different: the managed wait cancels promptly, while
the native callback state remains rooted until a later explicit poll delivers
the terminal callback.

`new GPUSamplerDescriptor()` supplies the WebGPU defaults, including
clamp-to-edge addressing, nearest filtering, LOD range 0–32, and anisotropy 1.
As with all C# structs, `default(GPUSamplerDescriptor)` bypasses those
initializers and is not a valid sampler descriptor.

Naga 27 rejects value returns nested in loops. The WGSL compiler therefore
lowers such returns through its existing typed control carrier and emits the
final value return after the loop. Shader sources and native dependencies stay
unchanged; module creation is covered by the smoke test and image equivalence
by the parity oracle below.

The `PortableWgsl` cooperation profile remains fail-closed for participating
functions containing loops; this workaround does not relax that admission rule
or claim that loop-return lowering is checked by the cooperation verifier.
Loop-free supported cooperative shaders continue through the existing verified
target path.

## Raymarch image parity oracle

The parity test reuses `RaymarchingPrimitiveShader` from the non-Web
`DualDrill.Shaders` assembly and compiles it through the public
`CLSLCompiler` CIL → Slang → WGSL path. It independently compiles the pinned,
pristine MIT-licensed Xds3zN GLSL reference in `Reference/` directly to WGSL
with `slangc`. Both pipelines execute on the same adapter/device with an
independent location-0 `vec2` reference vertex shader and the same six
fullscreen vertices. The GLSL wrapper receives `mainImage` into a
function-local color and then assigns the stage output so Slang does not emit
an invalid private-address-space pointer argument.

Four 320×180 RGBA8 profiles cover AA1/AA2/AA3 at time 1 with centered mouse,
plus AA1 at time 3 with off-center mouse. The candidate receives the actual
group-0 uniform bindings 0–3 (`iResolution`, `iTime`, `iMouse`, `iAA`).
Comparison is unmasked RGB over every pixel; alpha must be exactly 255.
Acceptance limits are MAE ≤ 1, RMSE ≤ 4, p99 ≤ 8, and max ≤ 64. Failures retain
raw RGBA8 and dependency-free PPM reference/candidate/diff files under the test
output's `oracle-failures/` directory.

`CilIndirectNegativeZeroNativeTests` uses the same pinned NVIDIA wrapper and
native compute/readback API. It compiles a forced-CIL, function-local `stind.r4`
then `ldind.r4` shader through the public compiler and checks the mapped f32
output bits are exactly `0x80000000`. This protects signed-zero literal
spelling across Slang and WGSL; it does not add general C# ref-local support.

Run source-integrity, comparator, and direct reference-compilation checks
without initializing native WebGPU:

```sh
nix develop --builders '' --command dotnet test \
  DualDrill.CLSL.NativeTest/DualDrill.CLSL.NativeTest.csproj \
  -c Release -r linux-x64 \
  --filter 'FullyQualifiedName~RaymarchOraclePureTests'
```

Run all native tests on the verified NVIDIA host:

```sh
NIXPKGS_ALLOW_UNFREE=1 nix develop --builders '' --command \
  nix run --builders '' --impure \
  github:nix-community/nixGL/b6105297e6f0cd041670c3e8628394d4ee247ed5#nixVulkanNvidia -- \
  timeout 300s dotnet test \
  DualDrill.CLSL.NativeTest/DualDrill.CLSL.NativeTest.csproj \
  -c Release -r linux-x64
```

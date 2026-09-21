# Native wgpu smoke test

This optional Linux x64 xUnit project compiles the existing minimum triangle
fixture through the public CLSL WGSL compiler, renders it with native wgpu to a
64×64 offscreen texture, and verifies two readback pixels. It needs no display,
surface, window, or browser.

It also renders input-selected scalar/vector early-return paths, creates a
native shader module from the canonical Compiler.Server raymarch source, and
verifies that invalid WGSL reports a managed diagnostic instead of throwing
across the native callback boundary. This remains a narrow baseline, not
general C#-to-WGSL equivalence or raymarching image-parity coverage.

Run it from the repository root with the pinned compiler shell and native wgpu
dependencies available. On the verified Ubuntu/NVIDIA host, nixGL intentionally
and non-hermetically exposes the existing proprietary Vulkan driver:

```sh
NIXPKGS_ALLOW_UNFREE=1 nix develop --command \
  nix run --impure \
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
unchanged; the canonical Compiler.Server raymarch must create a native shader
module without claiming full image parity.

The `PortableWgsl` cooperation profile remains fail-closed for participating
functions containing loops; this workaround does not relax that admission rule
or claim that loop-return lowering is checked by the cooperation verifier.
Loop-free supported cooperative shaders continue through the existing verified
target path.

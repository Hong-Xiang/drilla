# Native wgpu smoke test

This optional Linux x64 xUnit project compiles the existing minimum triangle
fixture through the public CLSL WGSL compiler, renders it with native wgpu to a
64×64 offscreen texture, and verifies two readback pixels. It needs no display,
surface, window, or browser.

It also verifies that invalid WGSL reports a managed diagnostic instead of
throwing across the native callback boundary. This is a narrow baseline, not
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
the normal `Unmap` in a `finally` block.

Naga 27 still rejects the current canonical CLSL raymarch output because of its
return-inside-loop validation bug. That shader remains explicitly unsupported
by the native backend. The corrected reference shader and ordinary rendering
shaders are accepted; changing compiler output or shader text is outside this
migration.

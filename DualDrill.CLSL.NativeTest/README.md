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

The bundled Evergine/wgpu-native pair remains unchanged. Its older Naga validator
does not accept all current WGSL emitted by Slang, so the browser demos are not
all supported by this native baseline. Browser WebGPU uses a separate
implementation. Native-provider migration and automated raymarching comparisons
are deferred; they are not prerequisites for using the compiler server.

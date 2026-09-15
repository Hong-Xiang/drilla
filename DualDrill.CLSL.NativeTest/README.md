# Native wgpu smoke test

This optional Linux x64 xUnit project compiles the existing minimum triangle
fixture through the public CLSL WGSL compiler, renders it with native wgpu to a
64×64 offscreen texture, and verifies two readback pixels. It needs no display,
surface, window, or browser.

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

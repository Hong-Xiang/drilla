# Native wgpu smoke test

This optional Linux x64 xUnit project compiles the compiler host's orange
triangle plus the existing Uniform, Mandelbrot, and Raymarching fixtures
through the public CLSL WGSL compiler. Each fixed profile attempts one frame
with native wgpu to a 64×64 offscreen texture and verifies readback pixels when
compilation and pipeline creation succeed. A compiler or native shader failure
fails the test rather than substituting another fixture. It needs no display,
surface, window, or browser.

The checks are execution and pipeline-binding smoke tests, not general C#
semantic-equivalence tests. The triangle and uniform profiles verify inside
and outside pixels (with one byte of tolerance for RGBA8 float rounding).
Fullscreen profiles clear to transparent and verify opaque output at points in
both quad triangles. Time is fixed at zero; no animation is exercised.

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

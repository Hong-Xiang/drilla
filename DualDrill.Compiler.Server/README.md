# DualDrill compiler server

This local-only host is separate from `DualDrill.Server` and its Windows/WebView
setup. It references only `DualDrill.ILSL`, provides a small triangle fixture,
and links the other three C# shader sources into the host. It does not initialize
a native GPU.

From the repository root:

```sh
nix develop --command pnpm --dir DualDrill.JS install --frozen-lockfile
nix develop --command pnpm --dir DualDrill.JS run compiler:build
nix develop --command dotnet run --project DualDrill.Compiler.Server
```

Open <http://127.0.0.1:5083/>. The frontend uses one browser WebGPU device to
compile and render the selected Triangle, Uniform, Mandelbrot, or Raymarching
fixture. Mandelbrot and Raymarching animate from time 0; Triangle and Uniform
render one frame because their C# fixtures have no time uniform. Switching
shaders resets elapsed time and releases the previous rendering resources.
Compiler, shader-module, pipeline, and device errors stay visible instead of
falling back to another shader. General reflection-driven rendering remains
intentionally out of scope: each known fixture has an explicit input profile.

Use a WebGPU-capable browser at the localhost URL. The browser supplies its own
WebGPU implementation; it does not load this project's bundled native wgpu
library. After changing the frontend, rebuild with `compiler:build` and reload
the page (hard-refresh if the old bundle is cached).

Frontend checks, after installing the locked dependencies above:

```sh
nix develop --command pnpm --dir DualDrill.JS run compiler:check
nix develop --command pnpm --dir DualDrill.JS run compiler:test
```

`compiler:test` rebuilds the bundle and exercises animation/resource lifetimes
with deterministic mocks. It is not a browser or GPU rendering test.

The shared C# type is now `DualDrill.Shaders.RaymarchingPrimitiveShader`, sourced
from `Shared/Shaders/RaymarchingPrimitiveShader.cs` and linked into the compiler
server, tests, and Engine. Callers using the former test/Engine namespaces must
update their imports. Vertex input remains location 0, `float32x2`.

The raymarching fixture keeps its existing shader and entry-point names and uses
these group 0 uniform bindings:

| Binding | WGSL type   | Value              |
| ------- | ----------- | ------------------ |
| 0       | `vec2<f32>` | Canvas resolution  |
| 1       | `f32`       | Time in seconds    |
| 2       | `vec4<f32>` | Mouse coordinates  |
| 3       | `i32`       | Antialiasing level |

Clients migrating from the two-binding shader must rebuild and reload their
bind group with the mouse and antialiasing buffers. The bundled client defaults
to fixed mouse coordinates at the canvas center, starts elapsed time at 0 on each
selection, and uses AA1. Mouse interaction and an AA selector are not exposed in
the demo UI.
The shader defensively bounds AA to 1–3; AA1 has no subpixel offset, while AA2
and AA3 use the reference sample offsets. The complete 22-primitive scene and
reference AO, shadow, back-light, and subsurface-lighting paths are restored,
but quantitative image parity against the independent GLSL reference is deferred.

The optional native project retains white `MinimumHelloTriangleShaderModule`
triangle readback and an invalid-WGSL diagnostic regression; the native triangle
is distinct from the orange host `MinimumTriangleShader`. The non-triangle
examples have no native execution-equivalence coverage. Their current WGSL uses
numeric `bool(...)` conversions permitted by the WGSL specification and accepted
by the bundled Naga 0.19.2 consumer.
Raymarching's generated shader and reflection paths are covered in both Debug
and Release. The bundled native Naga lacks required current-WGSL features;
modern native integration and independent image-parity validation are deferred.
The unmerged oracle is tracked in [#92](https://github.com/Hong-Xiang/drilla/pull/92)
and provider investigation in [#93](https://github.com/Hong-Xiang/drilla/pull/93);
neither is required for manual browser testing.

Generated IR, Slang, and WGSL are available at
`/ilsl/compile/{shader-name}/{target}`. Reflection is at
`/ilsl/reflect/{shader-name}`. The accepted shader names are
`MinimumTriangleShader`, `SimpleStructUniformShaderModule`,
`MandelbrotDistanceShaderModule`, and `RaymarchingPrimitiveShader`.

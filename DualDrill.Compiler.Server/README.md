# DualDrill compiler server

This local-only host leaves `DualDrill.Server` and its Windows/WebView setup
unchanged. It references only `DualDrill.ILSL`, links four existing C# shader
fixtures into the host, and does not initialize a native GPU.

From the repository root:

```sh
nix develop --command pnpm --dir DualDrill.JS install --frozen-lockfile
nix develop --command pnpm --dir DualDrill.JS run compiler:build
nix develop --command dotnet run --project DualDrill.Compiler.Server
```

Open <http://127.0.0.1:5083/>. The frontend uses one browser WebGPU device to
compile and attempt one frame of the selected Triangle, Uniform, Mandelbrot, or
Raymarching fixture. Compiler, shader-module, pipeline, and device errors stay
visible instead of falling back to another shader. Animation and general
reflection-driven rendering are intentionally out of scope: each known fixture
has a fixed input profile.

The raymarching fixture keeps its existing shader and entry-point names and uses
these group 0 uniform bindings:

| Binding | WGSL type | Value |
| --- | --- | --- |
| 0 | `vec2<f32>` | Canvas resolution |
| 1 | `f32` | Time in seconds |
| 2 | `vec4<f32>` | Mouse coordinates |
| 3 | `i32` | Antialiasing level |

Clients migrating from the two-binding shader must rebuild and reload their
bind group with the mouse and antialiasing buffers. The bundled client defaults
to the canvas center, time 0, and AA1. The shader defensively bounds AA to 1–3;
AA1 has no subpixel offset, while AA2 and AA3 use the reference sample offsets.
The complete 22-primitive scene and reference AO, shadow, back-light, and
subsurface-lighting paths are restored, but image parity remains pending native
validation against the independent GLSL reference.

Only the existing white `MinimumHelloTriangleShaderModule` native smoke test is
retained; it is distinct from the orange host `MinimumTriangleShader`. The
three new examples are Debug demo profiles, not native execution-equivalence
coverage. Their current WGSL uses numeric `bool(...)` conversions permitted by
the WGSL specification and accepted by the bundled Naga 0.19.2 consumer.
Raymarching's generated shader and reflection paths are covered in both Debug
and Release. The bundled native Naga lacks required current-WGSL features;
modern native integration and independent image-parity validation remain
pending. Provider investigation is tracked in #93.

Generated IR, Slang, and WGSL are available at
`/ilsl/compile/{shader-name}/{target}`. Reflection is at
`/ilsl/reflect/{shader-name}`. The accepted shader names are
`MinimumTriangleShader`, `SimpleStructUniformShaderModule`,
`MandelbrotDistanceShaderModule`, and `RaymarchingPrimitiveShader`.

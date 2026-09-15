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

Generated IR, Slang, and WGSL are available at
`/ilsl/compile/{shader-name}/{target}`. Reflection is at
`/ilsl/reflect/{shader-name}`. The accepted shader names are
`MinimumTriangleShader`, `SimpleStructUniformShaderModule`,
`MandelbrotDistanceShaderModule`, and `RaymarchingPrimitiveShader`.

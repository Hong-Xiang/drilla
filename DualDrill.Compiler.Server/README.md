# DualDrill compiler server

This local-only host leaves `DualDrill.Server` and its Windows/WebView setup
unchanged. It references only `DualDrill.ILSL`, serves the minimum C# triangle,
and does not initialize a native GPU.

From the repository root:

```sh
nix develop --command pnpm --dir DualDrill.JS install --frozen-lockfile
nix develop --command pnpm --dir DualDrill.JS run compiler:build
nix develop --command dotnet run --project DualDrill.Compiler.Server
```

Open <http://127.0.0.1:5083/>. The frontend requests generated WGSL from
`/ilsl/compile/MinimumTriangleShader/wgsl` and uses the browser's default
WebGPU adapter. IR and Slang are available by replacing the final path segment;
reflection is at `/ilsl/reflect/MinimumTriangleShader`.

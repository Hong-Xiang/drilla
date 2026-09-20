# drilla

Drilla engine for HPC and visualization

For compiler development on Linux without WebView2, use the
[compiler-only server](DualDrill.Compiler.Server/README.md). Its browser demo
offers Triangle, Uniform, animated Mandelbrot, and animated Raymarching shaders
compiled from C# through Slang to WGSL.

The isolated [.NET CPU-to-WebRTC proof of concept](DualDrill.Media.Server/README.md)
streams managed BGRA frames through GStreamer to a local browser.

## develop

requirements:

- [Node.js](https://nodejs.org/en) and [pnpm](https://pnpm.io/)
- [dotnet 9.0](https://dotnet.microsoft.com/en-us/download)
- [slangc](https://github.com/shader-slang/slang) on `PATH` (the Nix package
  is `shader-slang`; the unrelated `slang` package does not provide it), or
  installed through the Vulkan SDK

### Linux Nix toolchain

On x86-64 Linux, enter the pinned compiler development shell from the repository root:

```sh
nix develop
```

The shell supplies .NET SDK/runtime 9, Slang (`slangc`), LLVM 16 native
libraries for LLVMSharp, the Vulkan loader needed by native WebGPU bindings,
Node.js, and pnpm. It preserves the host Vulkan ICD/driver environment and
any inherited `LD_LIBRARY_PATH`; it does not install or select Vulkan tools,
an ICD, software renderer, browser, or Chromium.

The shared MSBuild settings use the canonical `Directory.Build.props` filename
so they are discovered on case-sensitive filesystems as well as Windows.

The default shell is the compiler-only environment. On the tested Ubuntu
NVIDIA host, run the optional native graphics smoke through nixGL so the
Nix-built process can use the existing proprietary Vulkan driver:

```sh
NIXPKGS_ALLOW_UNFREE=1 nix develop --command \
  nix run --impure \
  github:nix-community/nixGL/b6105297e6f0cd041670c3e8628394d4ee247ed5#nixVulkanNvidia -- \
  timeout 120s dotnet test \
  DualDrill.CLSL.NativeTest/DualDrill.CLSL.NativeTest.csproj \
  -c Release -r linux-x64 --logger 'console;verbosity=minimal'
```

This wrapper exposes the host NVIDIA driver to the process; it does not
install an ICD or force a software fallback. Other driver vendors need their
corresponding, separately verified host interop rather than an assumed
fallback. The pinned nixGL source makes the wrapper reproducible, while
`--impure` is intentional because the existing host driver remains outside
the Nix closure; this graphics path is therefore not fully hermetic.

Inside `nix develop`, test the compiler project headlessly in both configurations.
The compiler reads the built CIL directly, so Release optimization can expose a
different control-flow graph than Debug:

```sh
dotnet test DualDrill.CLSL.Test/DualDrill.CLSL.Test.csproj -c Debug
dotnet test DualDrill.CLSL.Test/DualDrill.CLSL.Test.csproj -c Release
```

This is a reproducible development shell, not a fully hermetic Nix build:
NuGet restore still uses the configured package sources and cache. The
Windows-only solution projects are intentionally outside this Linux workflow;
use an existing browser for interactive development.

### Windows server development

The original Windows/WebView server remains separate from the Linux compiler
host. For that development path:

- Open `Drilla.slnx`, run `DualDrill.Server` project to start a backend server

- _optional_ In `DualDrill.JS`, run `node .\esbuild.mjs --watch` to rebuild TypeScript on changes

- Open browser, visit `https://localhost:7117/desktop` for basic rendering.

- visit `https://localhost:7117/ilsl` for basic C# IL to shader translation development

- _optional_ add `DUALDRILL_DATA_ROOT` to environment variable for mesh/texture data

When specifying a .NET runtime identifier, use a RID such as `win-x64`.
`x64` and `Any CPU` are platform settings, not runtime identifiers.

## CLSL (previously ILSL)

CLSL is a restricted C# shader language with attributes for shader stages,
resources, and built-ins. The active runtime-reflection compiler reads compiled
.NET CIL, lowers it through a typed IR to Slang, then invokes `slangc` to produce
WGSL. The public `CLSLCompiler` exposes IR, Slang, and WGSL output targets.

Other backends, including CUDA and LLVM-based SIMD execution, remain development
goals rather than supported outputs of this pipeline.

Features:

- [x] WGSL backend
- [x] control flow: if/loop
- [x] function scope variable declaration
- [x] primitive arithmetic/bitwise/logical/relational operation
- [x] basic primitive type mapping
- [x] vector type mapping
- [x] some vector based intrinsic functions
- [ ] matrix type
- [ ] array type
- [ ] texture type/sampler type
- [ ] read/write buffers
- [ ] auto detect custom helper functions without additional attributes
- [x] shader stage attributes
- [x] group/binding attributes
- [x] custom struct declaration
- [x] shader reflection for uniform/vertex buffer layouts
- [ ] C# getter/setters
- [ ] LLVM backend (for vectorization) and ideally use CIL -> LLVM auto vectorization -> CIL (intrinsics)
- [ ] CUDA backend

## Current scope and limitations

The Linux compiler host and animated browser demos are available without the
Windows server. The canonical raymarching shader lives in
`Shared/Shaders/RaymarchingPrimitiveShader.cs`; see the compiler-server guide for
its namespace and uniform-binding migration.

Control-flow support covers the exercised C# subset, not arbitrary CIL or a
complete control-flow reconstruction algorithm. Shader compilation and manual
browser rendering do not establish general semantic equivalence. The optional
native project covers triangle readback and error reporting; automated
raymarching image comparisons and a modern native-backend migration are deferred.

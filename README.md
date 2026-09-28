# drilla

Drilla engine for HPC and visualization

For browser-side shader development, use the
[compiler-only server](DualDrill.Compiler.Server/README.md). Its browser demo
offers Triangle, Uniform, animated Mandelbrot, and animated Raymarching shaders
compiled from C# through Slang to WGSL.

The isolated [.NET GPU-to-WebRTC proof of concept](DualDrill.Media.Server/README.md)
renders with Rust wgpu-native, reads back BGRA frames, and streams them through
GStreamer to a local browser. The separate
[development API host](DualDrill.Server/README.md) retains mesh/data,
code-generation, reflection, and GPU-rendered PNG endpoints.
All three hosts target plain .NET 10; the WPF/WebView desktop host has been removed.

## develop

requirements:

- [Bun](https://bun.com/) 1.4.2 for frontend installs/scripts; Node.js 22 for
  Vite/esbuild tools and the independent Node/V8 WebAssembly fixture harness
- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download)
- [slangc](https://github.com/shader-slang/slang) on `PATH` (the Nix package
  is `shader-slang`; the unrelated `slang` package does not provide it), or
  installed through the Vulkan SDK

### Linux Nix toolchain

On x86-64 Linux, enter the pinned compiler development shell from the repository root:

```sh
nix develop
```

The shell supplies .NET SDK/runtime 10, Slang (`slangc`), LLVM 16 native
libraries for LLVMSharp, the Vulkan loader needed by native WebGPU bindings,
Bun 1.4.2 and Node.js 22. It preserves the host Vulkan ICD/driver environment and
any inherited `LD_LIBRARY_PATH`; it does not install or select Vulkan tools,
an ICD, software renderer, browser, or Chromium.

The shared MSBuild settings use the canonical `Directory.Build.props` filename
so they are discovered on case-sensitive filesystems as well as Windows.

The default shell supports compiler and development API work. On the tested Ubuntu
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
NuGet restore still uses the configured package sources and cache.
Use an existing browser for interactive development.

### Development API host

From the repository root, inside the default Nix shell:

```sh
dotnet build Drilla.slnx
dotnet run --project DualDrill.Server --no-launch-profile -- --urls http://127.0.0.1:5268
```

The build uses a frozen Bun install and the existing esbuild frontend bundle.
The API host initializes a native GPU at startup, even for `/health`; on the
NVIDIA Linux host, wrap its launch in the same nixGL command shown above.
The compiler-only host does not need a server-side GPU.

Open `/swagger` for the retained API surface. `/render/cube` renders a PNG;
interactive streaming and per-session input now belong to
`DualDrill.Media.Server`, not this host. The former `/home/desktop`,
`/home/webview2`, old signaling endpoints, and shared-buffer APIs return 404.
There is no compatibility redirect or shared desktop session.

`/home/volume` requires the external dataset described in the
[API host guide](DualDrill.Server/README.md). The retained ILSL editor and
reflection experiments are not all complete; use the compiler-only browser
demo for the supported shader-development path.

### Mathematics generation

The active C# generator is `DualDrill.Mathematics.CodeGen`. It references
`DualDrill.APIDefinition/DualDrill.ApiGen.csproj` and its `DMath` generators,
using CLSL type/operation definitions to produce `DualDrill.Mathematics/*.gen.cs`.
It is unrelated to the removed, obsolete `DualDrill.ApiGen/` directory.
The generator and checked-in mathematical types are retained.

Pass a target directory explicitly; it must contain `DualDrill.Mathematics.csproj`.
To inspect generation without overwriting the checked-in math, copy that project
file into an isolated scratch directory and run:

```sh
dotnet run --project DualDrill.Mathematics.CodeGen --no-launch-profile -- /path/to/scratch
```

Compare the output before replacing any checked-in files. Current generation has
pre-existing metadata differences from the checked-in sources, including vector
type/swizzle attributes; regeneration is not currently a byte-for-byte rebuild.
This migration does not overwrite those sources or remove their metadata.

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

The compiler host and animated browser demos run independently of the
GPU-backed API and media hosts. The canonical raymarching shader lives in
`Shared/Shaders/RaymarchingPrimitiveShader.cs`; see the compiler-server guide for
its namespace and uniform-binding migration.

Control-flow support covers the exercised C# subset, not arbitrary CIL or a
complete control-flow reconstruction algorithm. Shader compilation and manual
browser rendering do not establish general semantic equivalence. The optional
native project covers triangle readback, error reporting, and canonical
raymarching image parity against a pinned independent GLSL reference. The media
server demonstrates the wgpu-native GPU-to-WebRTC path with a fixed
per-connection choice of the default triangle or canonical raymarching scene.
Raymarch controls currently orbit horizontally because the canonical shader
does not use vertical pointer input.

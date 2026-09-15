# drilla

Drilla engine for HPC and visualization

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

The same shell can run commands for another worktree:

```sh
nix develop path:/home/xianghong/drilla-worktrees/nix-toolchain --command \
  dotnet test /path/to/worktree/DualDrill.CLSL.Test/DualDrill.CLSL.Test.csproj \
  -p:DirectoryBuildPropsPath=/home/xianghong/drilla-worktrees/nix-toolchain/Directory.Build.props
```

Restore and test the compiler project headlessly:

```sh
dotnet restore DualDrill.CLSL.Test/DualDrill.CLSL.Test.csproj
dotnet test DualDrill.CLSL.Test/DualDrill.CLSL.Test.csproj --no-restore
```

This is a reproducible development shell, not a fully hermetic Nix build:
NuGet restore still uses the configured package sources and cache. The
Windows-only solution projects are intentionally outside this Linux workflow;
use an existing browser for interactive development.

### run dev environment

#### dev loop

* Open `Drilla.sln`, run `DualDrill.Server` project to start a backend server

* _optional_ In `DualDrill.JS` directory, run `node .\esbuild.mjs --watch` so ts code gets rebuilt automaticall on change

* Open browser, visit `https://localhost:7117/desktop` for basic rendering. 

* visit `https://localhost:7117/ilsl` for basic C# IL to shader translation development

* _optional_ add `DUALDRILL_DATA_ROOT` to environment variable for mesh/texture data

NOTE when runtime identifier is required to build/run, i.e. we need use `x64` runtime identifier, `Any CPU` will not work

## CLSL (previously ILSL)

CLSL is a C# embedded language which is designed to be compiled to multiple shader language running on GPU and SIMD accelerated CPU.

It is designed to be a subset of C# language, with custom attributes to extend its semantic for shaders.

It it designed to be compiled into shader language like WGSL, SPIR-V and CUDA, and also can be compiled into SIMD accelerated CPU code like Unity's Burst Compiler (with help of LLVM's auto vectorization).

ILSL's compiler currently support runtime compilation of dotnet's bytecode CIL(MSIL) to WGSL source text.

Features:

- [x] WGSL backend
- [x] control flow: if/loop
- [x] function scope variable declaration
- [x] priitive arithmetic/bitwise/logical/relational operation
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
- [x] shader relfection for uniform/vertex buffer layouts
- [ ] C# getter/setters
- [ ] LLVM backend (for vectorization) and ideally use CIL -> LLVM auto vectorization -> CIL (intrinsics)
- [ ] CUDA backend


## Current Progress

Start Clean Legacy Code and Use New SSA based IR
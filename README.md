# drilla

Drilla engine for HPC and visualization

## develop

requirements:

- [Node.js](https://nodejs.org/en) and [pnpm](https://pnpm.io/)
- [dotnet 9.0](https://dotnet.microsoft.com/en-us/download)
- [slangc](https://github.com/shader-slang/slang) need to be installed and added to PATH, can be installed via slang or via Vulkan SDK

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
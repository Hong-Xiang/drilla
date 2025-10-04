# drilla

Drilla engine for HPC and visualization

## develop

requirements:

- [Node.js](https://nodejs.org/en) and [pnpm](https://pnpm.io/)
- [dotnet 8.0](https://dotnet.microsoft.com/en-us/download)


### run dev environment

#### onetime

* *Deprecated* Add `DUALDRILLFFMPEGPATH` environment variable, value should be `bin` folder of ffmpeg (with shared libraries) directory.
  On windows, it could be installed using `winget install "FFmpeg (Shared)" --version "6.1.1"`

#### dev loop

* Open `Drilla.sln`, run `DualDrill.Server` project to start a backend server

* _optional_ In `DualDrill.JS` directory, run `node .\esbuild.mjs --watch` so ts code gets rebuilt automaticall on change

* Open browser, visit `https://localhost:7117/desktop` for basic rendering. 

* visit `https://localhost:7117/ilsl` for basic C# IL to shader translation development

* _optional_ add `DUALDRILL_DATA_ROOT` to environment variable for mesh/texture data

NOTE when runtime identifier is required to build/run, i.e. we need use `x64` runtime identifier, `Any CPU` will not work

## ILSL

ILSL is a C# embeedded language which could (planning) be compiled to multiple shader language runnning on GPU.

ILSL code is a subset of valid C# code, with custom attribute to extend its semantic for shaders.

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
- [ ] CUDA backend




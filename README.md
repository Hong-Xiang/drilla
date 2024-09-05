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
# drilla

Drilla engine for HPC and visualization

## develop

requirements:

- [Node.js](https://nodejs.org/en) and [pnpm](https://pnpm.io/)
- [dotnet 9.0+](https://dotnet.microsoft.com/en-us/download)
- certificate files to serve https server, which is required for WebXR. You can use `mkcert` to generate it.


### run dev environment

#### onetime

* Add `DUALDRILLFFMPEGPATH` environment variable, value should be `bin` folder of ffmpeg (with shared libraries) directory.
  On windows, it could be installed using `winget install "FFmpeg (Shared)" --version "6.1.1"`

#### dev loop

* Enter `DualDrill.JS` directory, run `pnpm install` and `node .\esbuild.mjs` to build js code.

* Open `Drilla.sln`, run `DualDrill.Server` project to start a backend server

* Open browser, visit `https://localhost:7117/desktop` to see the result. 

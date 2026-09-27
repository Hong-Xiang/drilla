# Development API host

`DualDrill.Server` is a .NET 10 web host, not a desktop/WebView client. It
requires a GPU supported by wgpu-native and a working host graphics driver
**at startup**, including for health checks. The default Nix shell provides the
.NET and compiler tools; on the NVIDIA Linux host, use the pinned nixGL Vulkan
wrapper described in `DualDrill.Media.Server/README.md`. The frontend build
step must also complete before serving the JS-backed pages.

The retained routes include:

- `/health`, `/`, `/swagger`, `/swagger/v1/swagger.json`
- `/api/Mesh/{name}/meta`, `/vertex`, `/index` (`Cube`, `Quad`,
  `WebGPULogo`, `ScreenQuad`)
- `/api/ApiGen/webgpu/*`, `/api/DotnetReflection/*`,
  `/ILSL/compile/{name}/{target}`, and `/ILSL/reflect/{name}`
- `/render/cube` (PNG rendered by the clear, triangle, logo, and cube GPU
  renderers, with device polling for asynchronous readback)
- `/render/repl`, `/home/volume`, `/ILSL` (browser developer pages)

`/api/Data/head` needs `DUALDRILL_DATA_ROOT` pointing to an existing dataset
directory containing `head256x256x109`. Without that external file, this
endpoint cannot serve data. Some experimental ILSL/reflection endpoints are
still unfinished; route registration is not a guarantee that each experiment
works.

The former `/home/desktop`, `/home/webview2`, `/api/SignalConnection/*`,
`/api/ServerConnection/*`, `/api/WebViewInterop/*`,
`/hub/signal-connection`, and `/ws/signal-connection/*` routes are retired
(404). Do not redirect them: interactive media is now served independently by
`DualDrill.Media.Server`. The WebView shared-buffer JS API and desktop project
have no replacements in this host.

For a private development port, pass `--urls http://127.0.0.1:58637` to the
application via `dotnet run --project DualDrill.Server -- --urls ...`; avoid
exposing this unauthenticated developer host on a public network.
With the host running, check API responses, browser pages, retired 404s, and a
decoded, non-uniform GPU PNG:

```sh
python3 script/verify-server-retirement.py http://127.0.0.1:58637
```

The dataset endpoint is deliberately excluded until the external data is
available.

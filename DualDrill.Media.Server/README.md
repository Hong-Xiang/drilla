# DualDrill.Media.Server

Local-only .NET 10 proof of concept for:

```text
C# CPU BGRA pixels
  -> appsrc
  -> videoconvert
  -> vp8enc
  -> rtpvp8pay
  -> webrtcbin
  -> browser <video>
```

This prototype does not reference the existing Engine, WebView, JavaScript, or
server projects. It allocates and copies one 320x240 BGRA `Gst.Buffer` per frame;
it is not zero-copy and makes no cross-platform support claim beyond environments
where it has actually been run.

## Requirements

- .NET SDK 10
- GStreamer 1.24 or newer (GstSharp.Net 1.28.13 targets the 1.24 API floor)
- Plugins providing `appsrc`, `videoconvert`, `vp8enc`, `rtpvp8pay`,
  `webrtcbin`, `nicesrc`, and `dtlssrtpenc`
- A browser with WebRTC support

On x86-64 Linux, the pinned `media` Nix shell supplies .NET, GStreamer, the
required plugins, and Chromium. The default compiler shell is unchanged.
Outside that shell, install the native runtime and plugins separately; the
NuGet packages contain managed bindings, not GStreamer itself.

## Run

```sh
nix develop --builders '' .#media
dotnet run --project DualDrill.Media.Server/DualDrill.Media.Server.csproj
```

Open <http://127.0.0.1:5084/> and select **Start**. The acceptance harness may
rely on stable element IDs `#video`, `#status`, `#start`, and `#stop`; signaling
uses `ws://127.0.0.1:5084/ws`.

Only one WebSocket viewer is admitted. A second handshake receives HTTP 409.
Stopping, disconnecting, or closing the page tears the pipeline down to
`GST_STATE_NULL` before callbacks, promises, and native owners are released.
Reconnect creates a fresh session and pipeline.

The application validates signaling JSON and message sizes, queues trickled ICE
until the corresponding remote description exists, bounds appsrc to two buffers,
and fails negotiation if no valid browser answer completes within 20 seconds.

The dependency-free CPU generator check does not require GStreamer:

```sh
dotnet run --project DualDrill.Media.Server/DualDrill.Media.Server.csproj -- --self-test
```

The native frame check exercises the same `CpuBgraInput` with an `appsink`. It
overwrites caller memory immediately after submission, then pauses production
and confirms that the next frame receives a current live timestamp:

```sh
dotnet run --project DualDrill.Media.Server/DualDrill.Media.Server.csproj -- --native-self-test
```

## Browser acceptance

With the server running, start a separate terminal in the media shell:

```sh
env -u LD_LIBRARY_PATH chromium \
  --headless=new --no-sandbox --disable-gpu --disable-dev-shm-usage \
  --no-proxy-server --password-store=basic \
  --disable-background-networking --disable-component-update \
  --autoplay-policy=no-user-gesture-required \
  --remote-debugging-address=127.0.0.1 --remote-debugging-port=19223 \
  --user-data-dir="$(mktemp -d -t drilla-media-browser.XXXXXX)" about:blank
```

This browser profile is disposable and must not contain personal credentials.
The basic password store avoids a desktop-keyring prompt in headless sessions;
removing the media shell's library path keeps GStreamer libraries out of
Chromium's loader. The browser uses software rendering: this is not a GPU or
hardware-decoder benchmark. Close it after the check.

In another media-shell terminal:

```sh
dotnet run -p:ImportDirectoryPackagesProps=false script/gstsharp-browser-smoke.cs -- \
  http://127.0.0.1:5084/ http://127.0.0.1:19223/
```

The dependency-free .NET script drives Chromium's DevTools protocol. It checks
decoded dimensions, the positions of four large color regions, the movement of
a white marker, rejection of a concurrent viewer, and recovery after closing a
tab. It also delivers delayed callbacks from a stopped connection after its
replacement starts. Color comparisons allow for VP8 loss. Its restore is isolated from the
repository's unrelated central NuGet declarations.

The browser module is plain JavaScript checked with the TypeScript version
already pinned in `DualDrill.JS/pnpm-lock.yaml`:

```sh
bun x --package typescript@5.3.3 tsc --allowJs --checkJs --noEmit --strict \
  --noUncheckedIndexedAccess --target es2022 --moduleDetection force \
  --lib es2022,dom DualDrill.Media.Server/wwwroot/client.js
```

## CPU frame ownership

`CpuBgraInput.Push` synchronously copies exactly one tightly packed 320x240 BGRA
frame into a mapped GStreamer-owned buffer, unmaps it, and transfers the buffer
to `appsrc`. The caller may reuse its input bytes as soon as the call returns.
The demo generator is only a source of these bytes; no GPU or browser canvas is
involved in producing the outgoing video. Connecting existing renderer readback
to this input is a separate integration step.

# DualDrill.Media.Server

Loopback-by-default .NET 10 proof of concept for:

```text
C# -> Rust wgpu-native hardware GPU
  -> animated triangle in a BGRA8Unorm texture
  -> serial GPU-to-CPU readback
  -> appsrc
  -> videoconvert
  -> vp8enc
  -> rtpvp8pay
  -> webrtcbin
  -> browser <video>
```

This prototype references only the shared Graphics project, not the existing
Engine, WebView, JavaScript, or server projects. It reuses one offscreen texture
and staging buffer, then copies one tightly packed BGRA frame into a
GStreamer-owned buffer. The default is 320x240 at 30 fps. This is not zero-copy.
Software/unknown adapters are rejected rather than silently replacing GPU
rendering.

The modern backend uses the matched Alimer managed/native packages and Rust
wgpu-native, not Dawn. The demo deliberately uses a small standalone WGSL scene:
the compiler now emits the canonical CLSL raymarch as a native-accepted shader
module despite Naga's return-in-loop limitation, but the media demo remains the
independent animated triangle. Native module acceptance does not establish
raymarch image parity or integrate that scene into the stream.

## Requirements

- .NET SDK 10
- A hardware GPU supported by wgpu-native and its OS driver/runtime
- GStreamer 1.24 or newer (GstSharp.Net 1.28.13 targets the 1.24 API floor)
- Plugins providing `appsrc`, `videoconvert`, `vp8enc`, `rtpvp8pay`,
  `webrtcbin`, `nicesrc`, and `dtlssrtpenc`
- A browser with WebRTC support

On x86-64 Linux, the pinned `media` Nix shell supplies .NET, the Vulkan loader,
GStreamer, required plugins, and Chromium. The default compiler shell is unchanged.
The graphics NuGet packages include the matched native wgpu library, but not the
host GPU driver. GstSharp.Net includes managed bindings, not GStreamer itself.
Outside the shell, install the native GStreamer runtime and plugins separately.

## Run

```sh
nix develop --builders '' .#media
dotnet run --project DualDrill.Media.Server/DualDrill.Media.Server.csproj
```

On the Ubuntu/NVIDIA host, use the existing pinned nixGL wrapper to expose the
host Vulkan driver to Nix. This does not install a driver or force software rendering:

```sh
NIXPKGS_ALLOW_UNFREE=1 nix develop --builders '' .#media --command \
  nix run --builders '' --impure \
  github:nix-community/nixGL/b6105297e6f0cd041670c3e8628394d4ee247ed5#nixVulkanNvidia -- \
  dotnet run --project DualDrill.Media.Server/DualDrill.Media.Server.csproj
```

Open <http://127.0.0.1:5084/> and select **Start**. The acceptance harness may
rely on stable element IDs `#video`, `#status`, `#stats`, `#start`, and `#stop`;
signaling uses `ws://127.0.0.1:5084/ws`.

Width, height, frame rate, and an optional VP8 target bitrate are ordinary .NET
configuration keys. Dimensions must be positive and even. Frame rate must be an
integer from 1 through 120 fps and defaults to 30 fps. Bitrate is in bits per
second; omitting it preserves `vp8enc`'s native default instead of guessing a
target. To try 1920x1080 at 60 fps and an explicit 8 Mbps target on the trusted
LAN interface while retaining loopback:

```sh
NIXPKGS_ALLOW_UNFREE=1 nix develop --builders '' .#media --command \
  nix run --builders '' --impure \
  github:nix-community/nixGL/b6105297e6f0cd041670c3e8628394d4ee247ed5#nixVulkanNvidia -- \
  dotnet run --project DualDrill.Media.Server/DualDrill.Media.Server.csproj -- \
  --Video:Width=1920 --Video:Height=1080 --Video:FramesPerSecond=60 \
  --Video:Bitrate=8000000 \
  --urls "http://127.0.0.1:5084;http://10.172.211.158:5084"
```

Only opt in to a non-loopback URL on a trusted LAN. This prototype has no
authentication or TLS and must not be exposed to public networks or public IPv6.
Open `http://10.172.211.158:5084/` from the receiving machine. The page reports
decoded dimensions and frame rate plus the rate of received video RTP payload
bytes from successive WebRTC statistics snapshots. That measured rate excludes
IP/UDP/ICE/DTLS and other wire overhead. It is not the configured encoder target:
the simple rotating triangle can compress far below 8 Mbps.

Only one WebSocket viewer is admitted. A second handshake receives HTTP 409.
Each session logs its hardware adapter and owns its GPU resources. Stopping,
disconnecting, or closing the page cancels and drains production, tears the
pipeline down to `GST_STATE_NULL`, then releases the native owners.
Reconnect creates a fresh session, renderer, and pipeline.

The application validates signaling JSON and message sizes, queues trickled ICE
until the corresponding remote description exists, bounds appsrc to two buffers,
and fails negotiation if no valid browser answer completes within 20 seconds.

The CPU diagnostic generator and signaling check do not initialize GStreamer or
the GPU; production streaming never uses this generator:

```sh
dotnet run --project DualDrill.Media.Server/DualDrill.Media.Server.csproj -- --self-test
```

The native frame check exercises the same `CpuBgraInput` with an `appsink`. It
overwrites caller memory immediately after submission, then pauses production
and confirms that the next frame receives a current live timestamp:

```sh
dotnet run --project DualDrill.Media.Server/DualDrill.Media.Server.csproj -- --native-self-test
```

The GPU self-test does not initialize GStreamer. Run it with the same GPU runtime
and nixGL environment as the server:

```sh
dotnet run --project DualDrill.Media.Server/DualDrill.Media.Server.csproj -- --gpu-self-test
```

It renders at width 65 to exercise 260-byte rows padded to 512 bytes, verifies
BGRA channel order and opaque alpha, observes different animation frames, and
renders the initial state again with the same resources.

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

The optional final arguments select expected decoded dimensions, for example
`1920 1080`; omitting them preserves the 320x240 default.

The dependency-free .NET script drives Chromium's DevTools protocol. It checks
decoded dimensions, the colored triangle against its dark background, movement
between distinct decoded frames, at least ten seconds of finite receiver
bitrate/frame-rate samples per connection, rejection of a concurrent viewer, and
recovery after closing a tab. It also holds a statistics request and delivers
delayed callbacks from a stopped connection after its replacement starts,
requiring a different stream with advancing frame metadata and fresh statistics.
Color and motion comparisons allow for VP8 loss. Its restore is isolated from
the repository's unrelated central NuGet declarations. Browser software
decoding/rendering is independent of the server's hardware GPU source.

This verifies the transport mechanics and exposes receiver measurements; the
low-complexity triangle is not a network-capacity or complex-scene quality
benchmark. Neither Windows nor a real inter-host LAN path is claimed here.

The browser module is plain JavaScript checked with the TypeScript version
already pinned in `DualDrill.JS/pnpm-lock.yaml`:

```sh
bun x --package typescript@5.3.3 tsc --allowJs --checkJs --noEmit --strict \
  --noUncheckedIndexedAccess --target es2022 --moduleDetection force \
  --lib es2022,dom DualDrill.Media.Server/wwwroot/client.js
```

## CPU frame ownership

`CpuBgraInput.Push` synchronously copies exactly one tightly packed configured
BGRA frame into a mapped GStreamer-owned buffer, unmaps it, and transfers the
buffer to `appsrc`. The caller may reuse its input bytes as soon as the call
returns. GPU production is serialized: draw, copy, poll/map, remove row padding,
unmap, then submit the CPU frame. The next frame never copies into a still-mapped
staging buffer. Readback cancellation is drained before disposing its GPU owners.
There is no shared texture handle crossing into GStreamer or a browser canvas,
and no readback ring or implicit frame-ownership protocol.

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

The repository's native media development environment supplies these separately;
the project does not install native packages.

## Run

```sh
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

# Native wgpu smoke test

This optional Linux x64 xUnit project compiles the existing minimum triangle
fixture through the public CLSL WGSL compiler, renders it with native wgpu to a
64×64 offscreen texture, and verifies two readback pixels. It needs no display,
surface, window, or browser.

Run it from the repository root with the pinned .NET 9, Slang, Vulkan-loader,
and native wgpu dependencies available:

```sh
timeout 120s dotnet test DualDrill.CLSL.NativeTest/DualDrill.CLSL.NativeTest.csproj \
  -p:DirectoryBuildPropsPath="$PWD/Directory.build.props" \
  -c Release -r linux-x64
```

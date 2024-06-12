using Silk.NET.Core.Native;
using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using Silk.NET.Windowing;
using DualDrill.Graphics;
using DualDrill.Graphics.WebGPU.Native;
using Silk.NET.Core.Contexts;
using System.Runtime.CompilerServices;
using DualDrill.Server;
namespace DualDrill.Engine;

public static class WindowWebGPUNativeExtension
{
    public unsafe static WGPUSurfaceImpl* CreateWebGPUSurface(
        this INativeWindowSource view, WGPUInstanceImpl* instance
        )
    {
        WGPUSurfaceDescriptor descriptor = new();
        WGPUSurfaceDescriptorFromWindowsHWND surfaceDescriptorFromWindowsHWND = new();
        surfaceDescriptorFromWindowsHWND.chain = new WGPUChainedStruct
        {
            next = null,
            sType = WGPUSType.WGPUSType_SurfaceDescriptorFromWindowsHWND
        };
        surfaceDescriptorFromWindowsHWND.hwnd = (void*)(((IntPtr, IntPtr, IntPtr)?)view.Native.Win32).Value.Item1;
        surfaceDescriptorFromWindowsHWND.hinstance = (void*)(((IntPtr, IntPtr, IntPtr)?)view.Native.Win32).Value.Item3;
        WGPUSurfaceDescriptorFromWindowsHWND surfaceDescriptorFromWindowsHWND2 = surfaceDescriptorFromWindowsHWND;
        descriptor.nextInChain = (WGPUChainedStruct*)(&surfaceDescriptorFromWindowsHWND2);
        WGPUSurfaceImpl* result = WGPU.wgpuInstanceCreateSurface(instance, &descriptor);
        if (result is null)
        {
            throw new GraphicsApiException("Failed to create surface");
        }
        return result;
    }
}

public sealed class WebGPUNativeWindowService(WGPUProviderService DeviceProviderService) : BackgroundService
{

    //GPUInstanceW Instance { get; set; }
    //GPUAdapter Adapter { get; set; }
    GPUDevice Device { get; } = DeviceProviderService.Device;
    GPUQueue Queue { get; } = DeviceProviderService.Queue;

    //Graphics.ShaderModule Shader { get; set; }
    GPUShaderModule ShaderModule { get; set; }
    GPUPipelineLayout PipelineLayout { get; set; }
    GPURenderPipeline Pipeline { get; set; }

    GPUBuffer PixelBuffer { get; set; }
    GPUTexture RenderTarget { get; set; }
    GPUTextureView RenderTargetView { get; set; }

    readonly int Width = 512;
    readonly int Height = 512;
    uint BufferSize => (uint)(Width * Height * 4);

    private const string SHADER = @"@vertex
fn vs_main(@builtin(vertex_index) in_vertex_index: u32) -> @builtin(position) vec4<f32> {
    let x = f32(i32(in_vertex_index) - 1);
    let y = f32(i32(in_vertex_index & 1u) * 2 - 1);
    return vec4<f32>(x, y, 0.0, 1.0);
}

@fragment
fn fs_main() -> @location(0) vec4<f32> {
    return vec4<f32>(1.0, 1.0, 0.0, 1.0);
}";

    //public Silk.NET.WebGPU.WebGPU wgpu { get; } = Silk.NET.WebGPU.WebGPU.GetApi();
    //WebGPU(WebGPU.CreateDefaultContext(new string[] { "wgpu_native.dll" }));

    //WebGPU.GetApi();
    IWindow Window { get; set; }
    GPUSurface Surface { get; set; }

    GPUTextureFormat TextureFormat = GPUTextureFormat.BGRA8UnormSrgb;

    void CreateSwapChain()
    {
        //var config = new WGPUSurfaceConfiguration
        //{
        //    usage = (uint)(WGPUTextureUsage.WGPUTextureUsage_RenderAttachment | WGPUTextureUsage.WGPUTextureUsage_CopySrc),
        //    format = TextureFormat,
        //    presentMode = WGPUPresentMode.WGPUPresentMode_Fifo,
        //    device = Device.NativePointer,
        //    width = (uint)Window.FramebufferSize.X,
        //    height = (uint)Window.FramebufferSize.Y,
        //    alphaMode = WGPUCompositeAlphaMode.WGPUCompositeAlphaMode_Opaque,
        //};
        Surface.Configure(new GPUSurfaceConfiguration
        {
            Usage = GPUTextureUsage.RenderAttachment | GPUTextureUsage.CopySrc,
            Format = TextureFormat,
            PresentMode = GPUPresentMode.Fifo,
            Device = Device,
            Width = Window.FramebufferSize.X,
            Height = Window.FramebufferSize.Y,
            AlphaMode = GPUCompositeAlphaMode.Opaque,
        });
        //wgpu.SurfaceConfigure(Surface, &config);
    }

    //private static void DeviceLost(DeviceLostReason arg0, byte* arg1, void* arg2)
    //{
    //    Console.WriteLine($"Device lost! Reason: {arg0} Message: {SilkMarshal.PtrToString((nint)arg1)}");
    //}

    struct RequestResult
    {
        public nint Data { get; set; }
        public nint Message { get; set; }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    unsafe static void RequestAdaptorCallback(WGPURequestAdapterStatus status, WGPUAdapterImpl* adapter, sbyte* message, void* data)
    {
        var result = (RequestResult*)data;
        if (status == WGPURequestAdapterStatus.WGPURequestAdapterStatus_Success && result->Data == 0)
        {
            result->Data = (nint)adapter;
        }
        if (status != WGPURequestAdapterStatus.WGPURequestAdapterStatus_Success)
        {
            Console.WriteLine(Marshal.PtrToStringUTF8((nint)message));
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    unsafe static void RequestDeviceCallback(WGPURequestDeviceStatus status, WGPUDeviceImpl* adapter, sbyte* message, void* data)
    {
        var result = (RequestResult*)data;
        if (status == WGPURequestDeviceStatus.WGPURequestDeviceStatus_Success && result->Data == 0)
        {
            result->Data = (nint)adapter;
        }
        if (status != WGPURequestDeviceStatus.WGPURequestDeviceStatus_Success)
        {
            Console.WriteLine(Marshal.PtrToStringUTF8((nint)message));
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    unsafe struct WorkDoneData
    {
        public WGPUBufferImpl* Buffer { get; init; }
        public int BufferSize { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    unsafe static void QueueWorkDone(WGPUQueueWorkDoneStatus status, void* data)
    {
        var wd = (WorkDoneData*)data;
        WGPU.wgpuBufferMapAsync(wd->Buffer, (uint)WGPUMapMode.WGPUMapMode_Read, 0, (uint)wd->BufferSize, &BufferMapped, data);

    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    unsafe static void BufferMapped(WGPUBufferMapAsyncStatus status, void* data)
    {
        var wd = (WorkDoneData*)data;
        if (status == WGPUBufferMapAsyncStatus.WGPUBufferMapAsyncStatus_Success)
        {
            Console.WriteLine("Map succeed");
            var byteData = new Span<byte>(WGPU.wgpuBufferGetConstMappedRange(wd->Buffer, 0, (uint)wd->BufferSize), wd->BufferSize);
            var image = Image.LoadPixelData<SixLabors.ImageSharp.PixelFormats.Bgra32>(byteData, wd->Width, wd->Height);
            image.SaveAsPng("./wgpu-native-manual-binding-output.png");
            WGPU.wgpuBufferUnmap(wd->Buffer);
        }
        else
        {
            Console.WriteLine($"Map failed with status ${Enum.GetName(status)}");
        }
    }

    bool UsingWindow = false;

    private void WindowOnLoad()
    {
        //{
        //    var desc = new InstanceDescriptor
        //    {
        //    };
        //    Instance = wgpu.CreateInstance(ref desc);
        //}
        //{
        //    //WGPUInstanceDescriptor desc = new WGPUInstanceDescriptor();
        //    //WGPU.wgpuCreateInstance(&desc);
        //    Instance = new GPUInstanceW();
        //    //if (Instance is null)
        //    //{
        //    //    throw new Exception("Failed to create instance");
        //    //}
        //}

        if (UsingWindow)
        {
            Surface = GPUSurface.Create(Window, DeviceProviderService.Instance);
        }


        { //Get adapter
            //var requestAdapterOptions = new WGPURequestAdapterOptions
            //{
            //    compatibleSurface = Surface,
            //    //BackendType = BackendType.WebGpu,
            //    //PowerPreference = PowerPreference.HighPerformance
            //};

            //wgpu.InstanceRequestAdapter
            //(
            //    Instance,
            //    in requestAdapterOptions,
            //    new PfnRequestAdapterCallback((status, adapter, _, _) =>
            //    {
            //        if (status == RequestAdapterStatus.Success && Adapter is null)
            //        {
            //            Adapter = adapter;
            //        }
            //    }),
            //    null
            //);
            //Adapter = Instance.RequestAdapter(null);
            //delegate* unmanaged[Cdecl]<WGPURequestAdapterStatus, WGPUAdapterImpl*, sbyte*, void*, void> fptr = &RequestAdaptorCallback;

            ////)(Marshal.GetFunctionPointerForDelegate(RequestAdaptorCallback));
            //RequestResult result = new();
            //WGPU.wgpuInstanceRequestAdapter(
            //   Instance.NativePointer,
            //   &requestAdapterOptions,
            //   fptr, &result);
            //Adapter = (WGPUAdapterImpl*)(result.Data);
            //Console.WriteLine($"{nameof(Adapter)}{Adapter}");
            //Adapter.PrintInfo();
        } //Get adapter



        if (UsingWindow)
        {
            TextureFormat = Surface.PreferredFormat(DeviceProviderService.Adapter);
        }

        //Adapter.PrintFeatures();

        { //Get device
            //Device = Adapter.RequestDevice(new()
            //{
            //    DeviceLostCallback = new PfnDeviceLostCallback(DeviceLost),
            //});
            //WGPUDeviceDescriptor descriptor = new();
            //RequestResult result = new();
            //WGPU.wgpuAdapterRequestDevice(Adapter.NativePointer, &descriptor, &RequestDeviceCallback, &result);
            //Device = (WGPUDeviceImpl*)result.Data;
            //Device = Adapter.RequestDevice();
            //Console.WriteLine($"{nameof(Device)}{Device}");
            //Console.WriteLine($"Got device {(nuint)Device:X}");
        } //Get device


        { //Load shader
            //var wgslDescriptor = new WGPUShaderModuleWGSLDescriptor
            //{
            //    code = (sbyte*)SilkMarshal.StringToPtr(SHADER),
            //    chain = new WGPUChainedStruct
            //    {
            //        sType = WGPUSType.WGPUSType_ShaderModuleWGSLDescriptor
            //    }
            //};

            //var shaderModuleDescriptor = new WGPUShaderModuleDescriptor
            //{
            //    nextInChain = &wgslDescriptor.chain,
            //};

            ShaderModule = Device.CreateShaderModule(SHADER);
            //WGPU.wgpuDeviceCreateShaderModule(Device.NativePointer, &shaderModuleDescriptor);

            //Shader = Device.CreateShaderModule(SHADER);

            Console.WriteLine($"Created shader {ShaderModule}");
        } //Load shader

        //var surfaceCapabilities = new WGPUSurfaceCapabilities();
        //WGPU.wgpuSurfaceGetCapabilities(Surface.NativePointer, Adapter.NativePointer, &surfaceCapabilities);

        //var formats = new Span<WGPUTextureFormat>(surfaceCapabilities.formats, (int)surfaceCapabilities.formatCount);
        //foreach (var f in formats)
        //{
        //    Console.WriteLine($"Surface formats : {Enum.GetName(f)}");
        //}

        { //Create pipeline
            WGPUBlendState blendState = new()
            {
                color = {
                    srcFactor = WGPUBlendFactor.WGPUBlendFactor_One,
                    dstFactor = WGPUBlendFactor.WGPUBlendFactor_Zero,
                    operation = WGPUBlendOperation.WGPUBlendOperation_Add
                },
                //alpha = {
                //    srcFactor = BlendFactor.One,
                //    dstFactor = BlendFactor.Zero,
                //    operation = BlendOperation.Add
                //}
            };

            var colorTargetState = new WGPUColorTargetState
            {
                format = (WGPUTextureFormat)TextureFormat,
                //Blend = &blendState,
                writeMask = (uint)WGPUColorWriteMask.WGPUColorWriteMask_All
            };

            //var fragmentState = new WGPUFragmentState
            //{
            //    module = ShaderModule.NativePointer,
            //    targetCount = 1,
            //    targets = &colorTargetState,
            //    entryPoint = (sbyte*)SilkMarshal.StringToPtr("fs_main")
            //};

            {
                //var desc = new WGPUPipelineLayoutDescriptor();
                ////WGPU.wgpuDeviceCreatePipelineLayout(Device.NativePointer, &desc);
                PipelineLayout = Device.CreatePipelineLayout(new GPUPipelineLayoutDescriptor());
            }

            //var renderPipelineDescriptor = new WGPURenderPipelineDescriptor
            //{
            //    vertex =
            //    {
            //        module = ShaderModule.NativePointer,
            //        entryPoint = (sbyte*)SilkMarshal.StringToPtr("vs_main"),
            //    },
            //    primitive =
            //    {
            //        topology = WGPUPrimitiveTopology.WGPUPrimitiveTopology_TriangleList,
            //        //StripIndexFormat = IndexFormat.Undefined,
            //        //FrontFace = FrontFace.Ccw,
            //        //CullMode = CullMode.None
            //    },
            //    multisample = new WGPUMultisampleState
            //    {
            //        count = 1,
            //        mask = ~0u,
            //        //AlphaToCoverageEnabled = false
            //    },
            //    fragment = &fragmentState,
            //    //DepthStencil = null,
            //    layout = PipelineLayout.NativePointer
            //};

            //Pipeline = WGPU.wgpuDeviceCreateRenderPipeline(Device.NativePointer, &renderPipelineDescriptor);
            Pipeline = GPURenderPipeline.Create(Device, new GPURenderPipelineDescriptor()
            {
                Vertex = new GPUVertexState
                {
                    Module = ShaderModule,
                    EntryPoint = "vs_main"
                },
                Primitive = new GPUPrimitiveState
                {
                    Topology = GPUPrimitiveTopology.TriangleList
                },
                Multisample = new GPUMultisampleState
                {
                    Count = 1,
                    Mask = ~0u
                },
                Fragment = new GPUFragmentState
                {
                    Module = ShaderModule,
                    EntryPoint = "fs_main",
                    Targets = new[]{new GPUColorTargetState {
                        Format = TextureFormat,
                        WriteMask = (uint)GPUColorWriteMask.All
                    }}
                },
                Layout = PipelineLayout
            });


            Console.WriteLine($"Created pipeline {Pipeline}");
        } //Create pipeline

        {
            //WGPUBufferDescriptor descriptor = new()
            //{
            //    mappedAtCreation = 0,
            //    usage = (uint)(WGPUBufferUsage.WGPUBufferUsage_MapRead | WGPUBufferUsage.WGPUBufferUsage_CopyDst),
            //    size = (uint)(4 * Width * Height),
            //};
            RenderTarget = Device.CreateTexture(new()
            {
                Usage = GPUTextureUsage.RenderAttachment | GPUTextureUsage.CopySrc,
                Dimension = GPUTextureDimension.Dimension2D,
                Size = new GPUExtent3D()
                {
                    Width = Width,
                    Height = Height,
                    DepthOrArrayLayers = 1
                },
                Format = TextureFormat,
                MipLevelCount = 1,
                SampleCount = 1
            });

            RenderTargetView = RenderTarget.CreateView(new GPUTextureViewDescriptor()
            {
                BaseArrayLayer = 0,
                ArrayLayerCount = 1,
                BaseMipLevel = 0,
                MipLevelCount = 1,
                Aspect = GPUTextureAspect.All
            });

            PixelBuffer = Device.CreateBuffer(new()
            {
                Usage = GPUBufferUsage.MapRead | GPUBufferUsage.CopyDst,
                Size = 4ul * (ulong)Width * (ulong)Height,
            });
            //  WGPU.wgpuDeviceCreateBuffer(Device.NativePointer, &descriptor);
        }

        if (UsingWindow)
        {
            CreateSwapChain();
        }
    }

    bool IsFirstRender = true;
    bool IsRendering = false;

    double time = 0.0f;
    private async Task WindowOnRender(double delta)
    {
        Console.WriteLine("Render called");
        IsRendering = true;
        time += delta;
        //WGPUSurfaceTexture surfaceTexture;
        var surfaceTexture = Surface?.GetCurrentTexture();
        var texture = UsingWindow ? surfaceTexture.Value.Texture : RenderTarget;
        //WGPU.wgpuSurfaceGetCurrentTexture(Surface.NativePointer, &surfaceTexture);
        //switch (surfaceTexture.status)
        //{
        //    case WGPUSurfaceGetCurrentTextureStatus.WGPUSurfaceGetCurrentTextureStatus_Timeout:
        //    case WGPUSurfaceGetCurrentTextureStatus.WGPUSurfaceGetCurrentTextureStatus_Outdated:
        //    case WGPUSurfaceGetCurrentTextureStatus.WGPUSurfaceGetCurrentTextureStatus_Lost:
        //        // Recreate swapchain,
        //        WGPU.wgpuTextureRelease(surfaceTexture.texture);
        //        CreateSwapChain();
        //        // Skip this frame
        //        return;
        //    case WGPUSurfaceGetCurrentTextureStatus.WGPUSurfaceGetCurrentTextureStatus_OutOfMemory:
        //    case WGPUSurfaceGetCurrentTextureStatus.WGPUSurfaceGetCurrentTextureStatus_DeviceLost:
        //    case WGPUSurfaceGetCurrentTextureStatus.WGPUSurfaceGetCurrentTextureStatus_Force32:
        //        throw new Exception($"What is going on bros... {surfaceTexture.status}");
        //}


        var view = UsingWindow ? texture.CreateView(null) : RenderTargetView;
        //var view = RenderTargetView;

        //var commandEncoderDescriptor = new WGPUCommandEncoderDescriptor();

        //var encoder = WGPU.wgpuDeviceCreateCommandEncoder(Device.NativePointer, &commandEncoderDescriptor);
        var encoder = Device.CreateCommandEncoder(new());


        //var colorAttachment = new WGPURenderPassColorAttachment
        //{
        //    view = view,
        //    resolveTarget = null,
        //    loadOp = WGPULoadOp.WGPULoadOp_Clear,
        //    storeOp = WGPUStoreOp.WGPUStoreOp_Store,
        //    //DepthSlice = WebGPU.DepthSliceUndefined,
        //    clearValue = new()
        //    {
        //        r = (Math.Cos(time / 10.0f) + 1.0f) / 2,
        //        g = (Math.Sin(time / 10.0f) + 1.0f) / 2,
        //        b = 0,
        //        a = 1
        //    }
        //};

        //var renderPassDescriptor = new WGPURenderPassDescriptor
        //{
        //    colorAttachments = &colorAttachment,
        //    colorAttachmentCount = 1,
        //    depthStencilAttachment = null
        //};

        using var rp = encoder.BeginRenderPass(new()
        {
            ColorAttachments = (GPURenderPassColorAttachment[])[
                new GPURenderPassColorAttachment() {
                    View = view,
                    LoadOp = GPULoadOp.Clear,
                    StoreOp = GPUStoreOp.Store,
                    ClearValue = new() {
                R = (Math.Cos(time / 10.0f) + 1.0f) / 2,
                G = (Math.Sin(time / 10.0f) + 1.0f) / 2,
                B = 0,
                A = 1
                    }

                }
            ]
        });

        //var renderPass = WGPU.wgpuCommandEncoderBeginRenderPass(encoder, &renderPassDescriptor);

        rp.SetPipeline(Pipeline);
        rp.Draw(3, 1, 0, 0);
        rp.End();

        //WGPU.wgpuRenderPassEncoderSetPipeline(renderPass, Pipeline);
        //WGPU.wgpuRenderPassEncoderDraw(renderPass, 3, 1, 0, 0);
        //WGPU.wgpuRenderPassEncoderEnd(renderPass);

        //var queue = WGPU.wgpuDeviceGetQueue(Device.NativePointer);
        //using var queue = Device.GetQueue();
        var queue = Queue;
        //var desc = new WGPUCommandBufferDescriptor();
        //var commandBuffer = WGPU.wgpuCommandEncoderFinish(encoder.NativePointer, &desc);
        using var commandBuffer = encoder.Finish(new());
        //WGPUCommandBufferImpl* cmdb = commandBuffer.NativePointer;
        //WGPU.wgpuQueueSubmit(queue, 1, &cmdb);
        queue.Submit([commandBuffer]);

        //Surface.Present();



        if (IsFirstRender)
        {
            IsFirstRender = false;

            {
                //var source = new WGPUImageCopyTexture
                //{
                //    texture = surfaceTexture.Texture.NativePointer,
                //};
                //var destination = new WGPUImageCopyBuffer
                //{
                //    buffer = PixelBuffer.NativePointer,
                //    layout = new WGPUTextureDataLayout
                //    {
                //        bytesPerRow = (uint)(4 * Width),
                //        offset = 0,
                //        rowsPerImage = (uint)Height
                //    }
                //};
                //var copySize = new WGPUExtent3D
                //{
                //    width = (uint)Width,
                //    height = (uint)Height,
                //    depthOrArrayLayers = 1
                //};
                var encoderDesc = new WGPUCommandEncoderDescriptor { };
                using var e = Device.CreateCommandEncoder(new());
                e.CopyTextureToBuffer(new GPUImageCopyTexture
                {
                    Texture = texture
                }, new GPUImageCopyBuffer
                {
                    Buffer = PixelBuffer,
                    Layout = new GPUTextureDataLayout
                    {
                        BytesPerRow = 4 * Width,
                        Offset = 0,
                        RowsPerImage = Height
                    }
                }, new GPUExtent3D
                {
                    Width = Width,
                    Height = Height,
                    DepthOrArrayLayers = 1
                });

                //var encoder2 = WGPU.wgpuDeviceCreateCommandEncoder(Device.NativePointer, &encoderDesc);
                //WGPU.wgpuCommandEncoderCopyTextureToBuffer(e2.NativePointer,
                //     &source,
                //     &destination,
                //     &copySize);
                //var desc2 = new WGPUCommandBufferDescriptor { };
                using var cb = e.Finish(new());
                //var cmdBuffer = WGPU.wgpuCommandEncoderFinish(encoder2, &desc2);
                //WGPU.wgpuQueueSubmit(queue.NativePointer, 1, &cmdBuffer);
                queue.Submit([cb]);
                await queue.WaitSubmittedWorkDoneAsync().ConfigureAwait(false);

                Console.WriteLine("Queue work done");
                await PixelBuffer.MapAsync(GPUMapMode.Read, 0, (int)BufferSize).ConfigureAwait(true);
                Console.WriteLine("Map succeed");

                void SaveImage()
                {
                    var byteData = PixelBuffer.GetConstMappedRange(0, (int)BufferSize);
                    var image = Image.LoadPixelData<SixLabors.ImageSharp.PixelFormats.Bgra32>(byteData, Width, Height);
                    image.SaveAsPng("./wgpu-native-manual-binding-output.png");
                }

                SaveImage();
                PixelBuffer.Unmap();
            }

            //var wd = (WorkDoneData*)Marshal.AllocHGlobal(sizeof(WorkDoneData));
            //*wd = new WorkDoneData
            //{
            //    Buffer = PixelBuffer.NativePointer,
            //    BufferSize = (int)BufferSize,
            //    Width = Width,
            //    Height = Height
            //};


            //WGPU.wgpuQueueOnSubmittedWorkDone(queue.NativePointer, &QueueWorkDone, wd);
            //await queue.OnSubmittedWorkDoneAsync();
            //var tcs2 = new TaskCompletionSource();
            //queue.OnSubmittedWorkDone(tcs2.SetResult);
            //PixelBuffer.MapAsync2(GPUMapMode.Read, 0, (int)BufferSize);
            //queue.OnSubmittedWorkDone(async () =>
            //{
            //    Console.WriteLine("Queue work done");
            //    await PixelBuffer.MapAsync(GPUMapMode.Read, 0, (int)BufferSize).ConfigureAwait(true);
            //    Console.WriteLine("Map succeed");

            //    void SaveImage()
            //    {
            //        var byteData = PixelBuffer.GetConstMappedRange(0, (int)BufferSize);
            //        var image = Image.LoadPixelData<SixLabors.ImageSharp.PixelFormats.Bgra32>(byteData, Width, Height);
            //        image.SaveAsPng("./wgpu-native-manual-binding-output.png");
            //    }

            //    SaveImage();
            //    PixelBuffer.Unmap();
            //});
            //tcs2.Task.ContinueWith((t) =>
            //{
            //    Console.WriteLine("submitted done");
            //});
            //var tcs = new TaskCompletionSource();


            //tcs.SetResult();

            //await tcs.Task.ConfigureAwait(true);
        }

        if (UsingWindow)
        {
            Surface.Present();
            view.Dispose();
        }

        //WGPU.wgpuCommandBufferRelease(commandBuffer);
        //WGPU.wgpuRenderPassEncoderRelease(renderPass);
        //WGPU.wgpuCommandEncoderRelease(encoder);
        //WGPU.wgpuTextureViewRelease(view);
        //WGPU.wgpuTextureRelease(surfaceTexture.texture);
        //surfaceTexture.Texture.Dispose();

        IsRendering = false;
    }

    public void Render()
    {
        //SurfaceTexture surfaceTexture = default;
        //wgpu.SurfaceGetCurrentTexture(Surface, ref surfaceTexture);
        //switch (surfaceTexture.Status)
        //{
        //    case SurfaceGetCurrentTextureStatus.Timeout:
        //    case SurfaceGetCurrentTextureStatus.Outdated:
        //    case SurfaceGetCurrentTextureStatus.Lost:
        //        // Recreate swapchain,
        //        wgpu.TextureRelease(surfaceTexture.Texture);
        //        CreateSwapChain();
        //        // Skip this frame
        //        return;
        //    case SurfaceGetCurrentTextureStatus.OutOfMemory:
        //    case SurfaceGetCurrentTextureStatus.DeviceLost:
        //    case SurfaceGetCurrentTextureStatus.Force32:
        //        throw new Exception($"What is going on bros... {surfaceTexture.Status}");


        //}

        //var view = wgpu.TextureCreateView(surfaceTexture.Texture, null);

        //var commandEncoderDescriptor = new CommandEncoderDescriptor();

        //var encoder = wgpu.DeviceCreateCommandEncoder(Device.Handle, in commandEncoderDescriptor);

        //Console.WriteLine($"Texture view pointer: {(nint)TextureView}");
        //var colorAttachment = new RenderPassColorAttachment
        //{
        //    View = view,
        //    ResolveTarget = null,
        //    LoadOp = LoadOp.Clear,
        //    StoreOp = StoreOp.Store,
        //    ClearValue = new()
        //    {
        //        R = 0,
        //        G = 1,
        //        B = 0,
        //        A = 1
        //    }
        //};

        //var renderPassDescriptor = new RenderPassDescriptor
        //{
        //    ColorAttachments = &colorAttachment,
        //    ColorAttachmentCount = 1,
        //    DepthStencilAttachment = null
        //};

        //var renderPass = wgpu.CommandEncoderBeginRenderPass(encoder, in renderPassDescriptor);

        //wgpu.RenderPassEncoderSetPipeline(renderPass, Pipeline);
        //wgpu.RenderPassEncoderDraw(renderPass, 3, 1, 0, 0);
        //wgpu.RenderPassEncoderEnd(renderPass);

        //var queue = wgpu.DeviceGetQueue(Device.Handle);

        //var commandBufferDescriptor = new CommandBufferDescriptor();
        //var commandBuffer = wgpu.CommandEncoderFinish(encoder, in commandBufferDescriptor);
        //wgpu.QueueSubmit(queue, 1, &commandBuffer);

        //wgpu.SurfacePresent(Surface);
    }

    private void SaveTextureToImage()
    {
        //var waitEvent = new AutoResetEvent(false);

        //wgpu.BufferMapAsync(PixelBuffer.Ptr, MapMode.Read, 0, BufferSize, new PfnBufferMapCallback((status, data) =>
        //{
        //    waitEvent.Set();
        //}), default);

        //waitEvent.WaitOne();
        //var data = new Span<byte>(wgpu.BufferGetConstMappedRange(PixelBuffer.Ptr, 0, BufferSize), (int)BufferSize);

        //var image = SixLabors.ImageSharp.Image.LoadPixelData<SixLabors.ImageSharp.PixelFormats.Rgba32>(data, Width, Height);
        //image.SaveAsPng("./output.png");
        //wgpu.BufferUnmap(PixelBuffer.Ptr);
    }



    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        WindowOnLoad();
        await WindowOnRender(1.0).ConfigureAwait(false);
        //WindowOnRender(2.0);
        //await Task.Delay(10000);
        //WindowOnRender(1.0);
        return;
        //return;
        //var t = new Thread(() =>
        // {
        //     WindowOnRender(1.0);
        // });
        //t.Start();
        //await Task.Delay(10000);
        //await Task.Yield();
        //return;


        Window = Silk.NET.Windowing.Window.Create(WindowOptions.Default with
        {
            API = GraphicsAPI.None,
            Size = new(Width, Height),
            FramesPerSecond = 60,
            UpdatesPerSecond = 60,
            Title = "WebGPU Triangle",
            IsVisible = true,
            ShouldSwapAutomatically = true,
            IsContextControlDisabled = true,
        });

        Window.Load += WindowOnLoad;


        Window.Render += async (t) =>
        {
            if (stoppingToken.IsCancellationRequested)
            {
                Window.Invoke(() =>
                {
                    Window.Close();
                });
            }
            //Render();
            if (!IsRendering)
            {
                await WindowOnRender(t).ConfigureAwait(false);
            }
        };

        Window.Run();
    }
}

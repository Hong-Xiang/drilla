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

public unsafe sealed class WebGPUNativeWindowService : BackgroundService
{

    WGPUInstanceImpl* Instance { get; set; }
    WGPUAdapterImpl* Adapter { get; set; }
    WGPUDeviceImpl* Device { get; set; }

    //Graphics.ShaderModule Shader { get; set; }
    WGPUShaderModuleImpl* Shader { get; set; }
    WGPUPipelineLayoutImpl* PipelineLayout { get; set; }
    WGPURenderPipelineImpl* Pipeline { get; set; }

    WGPUTextureImpl* TargetTexture { get; set; }
    WGPUTextureViewImpl* TextureView { get; set; }

    WGPUBufferImpl* PixelBuffer { get; set; }

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
    WGPUSurfaceImpl* Surface { get; set; }

    WGPUTextureFormat TextureFormat = WGPUTextureFormat.WGPUTextureFormat_BGRA8UnormSrgb;

    public WebGPUNativeWindowService()
    {
    }
    void CreateSwapChain()
    {
        var config = new WGPUSurfaceConfiguration
        {
            usage = (uint)(WGPUTextureUsage.WGPUTextureUsage_RenderAttachment | WGPUTextureUsage.WGPUTextureUsage_CopySrc),
            format = TextureFormat,
            presentMode = WGPUPresentMode.WGPUPresentMode_Fifo,
            device = Device,
            width = (uint)Window.FramebufferSize.X,
            height = (uint)Window.FramebufferSize.Y,
            alphaMode = WGPUCompositeAlphaMode.WGPUCompositeAlphaMode_Opaque,
        };
        WGPU.wgpuSurfaceConfigure(Surface, &config);
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
    static void RequestAdaptorCallback(WGPURequestAdapterStatus status, WGPUAdapterImpl* adapter, sbyte* message, void* data)
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
    static void RequestDeviceCallback(WGPURequestDeviceStatus status, WGPUDeviceImpl* adapter, sbyte* message, void* data)
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
    struct WorkDoneData
    {
        public WGPUBufferImpl* Buffer { get; init; }
        public int BufferSize { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static void QueueWorkDone(WGPUQueueWorkDoneStatus status, void* data)
    {
        var wd = (WorkDoneData*)data;
        WGPU.wgpuBufferMapAsync(wd->Buffer, (uint)WGPUMapMode.WGPUMapMode_Read, 0, (uint)wd->BufferSize, &BufferMapped, data);

    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static void BufferMapped(WGPUBufferMapAsyncStatus status, void* data)
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

    private void WindowOnLoad()
    {
        //{
        //    var desc = new InstanceDescriptor
        //    {
        //    };
        //    Instance = wgpu.CreateInstance(ref desc);
        //}
        {
            WGPUInstanceDescriptor desc = new WGPUInstanceDescriptor();
            Instance = WGPU.wgpuCreateInstance(&desc);
            if (Instance is null)
            {
                throw new Exception("Failed to create instance");
            }
        }

        Surface = Window.CreateWebGPUSurface(Instance);


        { //Get adapter
            var requestAdapterOptions = new WGPURequestAdapterOptions
            {
                compatibleSurface = Surface,
                //BackendType = BackendType.WebGpu,
                //PowerPreference = PowerPreference.HighPerformance
            };

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
            delegate* unmanaged[Cdecl]<WGPURequestAdapterStatus, WGPUAdapterImpl*, sbyte*, void*, void> fptr = &RequestAdaptorCallback;

            //)(Marshal.GetFunctionPointerForDelegate(RequestAdaptorCallback));
            RequestResult result = new();
            WGPU.wgpuInstanceRequestAdapter(
               Instance,
               &requestAdapterOptions,
               fptr, &result);
            Adapter = (WGPUAdapterImpl*)(result.Data);
            Console.WriteLine($"Adapter {(nint)Adapter:X}");
            //Adapter.PrintInfo();
        } //Get adapter



        TextureFormat = WGPU.wgpuSurfaceGetPreferredFormat(Surface, Adapter);

        //Adapter.PrintFeatures();

        { //Get device
            //Device = Adapter.RequestDevice(new()
            //{
            //    DeviceLostCallback = new PfnDeviceLostCallback(DeviceLost),
            //});
            WGPUDeviceDescriptor descriptor = new();
            RequestResult result = new();
            WGPU.wgpuAdapterRequestDevice(Adapter, &descriptor, &RequestDeviceCallback, &result);
            Device = (WGPUDeviceImpl*)result.Data;
            Console.WriteLine($"Got device {(nuint)Device:X}");
        } //Get device


        { //Load shader
            var wgslDescriptor = new WGPUShaderModuleWGSLDescriptor
            {
                code = (sbyte*)SilkMarshal.StringToPtr(SHADER),
                chain = new WGPUChainedStruct
                {
                    sType = WGPUSType.WGPUSType_ShaderModuleWGSLDescriptor
                }
            };

            var shaderModuleDescriptor = new WGPUShaderModuleDescriptor
            {
                nextInChain = &wgslDescriptor.chain,
            };

            Shader = WGPU.wgpuDeviceCreateShaderModule(Device, &shaderModuleDescriptor);

            //Shader = Device.CreateShaderModule(SHADER);

            Console.WriteLine($"Created shader {(nuint)Shader:X}");
        } //Load shader

        var surfaceCapabilities = new WGPUSurfaceCapabilities();
        WGPU.wgpuSurfaceGetCapabilities(Surface, Adapter, &surfaceCapabilities);

        var formats = new Span<WGPUTextureFormat>(surfaceCapabilities.formats, (int)surfaceCapabilities.formatCount);
        foreach (var f in formats)
        {
            Console.WriteLine($"Surface formats : {Enum.GetName(f)}");
        }

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
                format = TextureFormat,
                //Blend = &blendState,
                writeMask = (uint)WGPUColorWriteMask.WGPUColorWriteMask_All
            };

            var fragmentState = new WGPUFragmentState
            {
                module = Shader,
                targetCount = 1,
                targets = &colorTargetState,
                entryPoint = (sbyte*)SilkMarshal.StringToPtr("fs_main")
            };

            {
                var desc = new WGPUPipelineLayoutDescriptor();
                PipelineLayout = WGPU.wgpuDeviceCreatePipelineLayout(Device, &desc);
            }

            var renderPipelineDescriptor = new WGPURenderPipelineDescriptor
            {
                vertex =
                {
                    module = Shader,
                    entryPoint = (sbyte*)SilkMarshal.StringToPtr("vs_main"),
                },
                primitive =
                {
                    topology = WGPUPrimitiveTopology.WGPUPrimitiveTopology_TriangleList,
                    //StripIndexFormat = IndexFormat.Undefined,
                    //FrontFace = FrontFace.Ccw,
                    //CullMode = CullMode.None
                },
                multisample = new WGPUMultisampleState
                {
                    count = 1,
                    mask = ~0u,
                    //AlphaToCoverageEnabled = false
                },
                fragment = &fragmentState,
                //DepthStencil = null,
                layout = PipelineLayout
            };

            Pipeline = WGPU.wgpuDeviceCreateRenderPipeline(Device, &renderPipelineDescriptor);

            Console.WriteLine($"Created pipeline {(nuint)Pipeline:X}");
        } //Create pipeline

        {
            WGPUBufferDescriptor descriptor = new()
            {
                mappedAtCreation = 0,
                usage = (uint)(WGPUBufferUsage.WGPUBufferUsage_MapRead | WGPUBufferUsage.WGPUBufferUsage_CopyDst),
                size = (uint)(4 * Width * Height),
            };
            PixelBuffer = WGPU.wgpuDeviceCreateBuffer(Device, &descriptor);
        }

        CreateSwapChain();
    }

    bool IsFirstRender = true;

    double time = 0.0f;
    private void WindowOnRender(double delta)
    {
        time += delta;
        WGPUSurfaceTexture surfaceTexture;
        WGPU.wgpuSurfaceGetCurrentTexture(Surface, &surfaceTexture);
        switch (surfaceTexture.status)
        {
            case WGPUSurfaceGetCurrentTextureStatus.WGPUSurfaceGetCurrentTextureStatus_Timeout:
            case WGPUSurfaceGetCurrentTextureStatus.WGPUSurfaceGetCurrentTextureStatus_Outdated:
            case WGPUSurfaceGetCurrentTextureStatus.WGPUSurfaceGetCurrentTextureStatus_Lost:
                // Recreate swapchain,
                WGPU.wgpuTextureRelease(surfaceTexture.texture);
                CreateSwapChain();
                // Skip this frame
                return;
            case WGPUSurfaceGetCurrentTextureStatus.WGPUSurfaceGetCurrentTextureStatus_OutOfMemory:
            case WGPUSurfaceGetCurrentTextureStatus.WGPUSurfaceGetCurrentTextureStatus_DeviceLost:
            case WGPUSurfaceGetCurrentTextureStatus.WGPUSurfaceGetCurrentTextureStatus_Force32:
                throw new Exception($"What is going on bros... {surfaceTexture.status}");
        }

        var view = WGPU.wgpuTextureCreateView(surfaceTexture.texture, null);

        var commandEncoderDescriptor = new WGPUCommandEncoderDescriptor();

        var encoder = WGPU.wgpuDeviceCreateCommandEncoder(Device, &commandEncoderDescriptor);


        var colorAttachment = new WGPURenderPassColorAttachment
        {
            view = view,
            resolveTarget = null,
            loadOp = WGPULoadOp.WGPULoadOp_Clear,
            storeOp = WGPUStoreOp.WGPUStoreOp_Store,
            //DepthSlice = WebGPU.DepthSliceUndefined,
            clearValue = new()
            {
                r = (Math.Cos(time / 10.0f) + 1.0f) / 2,
                g = (Math.Sin(time / 10.0f) + 1.0f) / 2,
                b = 0,
                a = 1
            }
        };

        var renderPassDescriptor = new WGPURenderPassDescriptor
        {
            colorAttachments = &colorAttachment,
            colorAttachmentCount = 1,
            depthStencilAttachment = null
        };

        var renderPass = WGPU.wgpuCommandEncoderBeginRenderPass(encoder, &renderPassDescriptor);

        WGPU.wgpuRenderPassEncoderSetPipeline(renderPass, Pipeline);
        WGPU.wgpuRenderPassEncoderDraw(renderPass, 3, 1, 0, 0);
        WGPU.wgpuRenderPassEncoderEnd(renderPass);

        var queue = WGPU.wgpuDeviceGetQueue(Device);
        var desc = new WGPUCommandBufferDescriptor();
        var commandBuffer = WGPU.wgpuCommandEncoderFinish(encoder, &desc);
        WGPU.wgpuQueueSubmit(queue, 1, &commandBuffer);




        if (IsFirstRender)
        {
            IsFirstRender = false;

            {
                var source = new WGPUImageCopyTexture
                {
                    texture = surfaceTexture.texture,
                };
                var destination = new WGPUImageCopyBuffer
                {
                    buffer = PixelBuffer,
                    layout = new WGPUTextureDataLayout
                    {
                        bytesPerRow = (uint)(4 * Width),
                        offset = 0,
                        rowsPerImage = (uint)Height
                    }
                };
                var copySize = new WGPUExtent3D
                {
                    width = (uint)Width,
                    height = (uint)Height,
                    depthOrArrayLayers = 1
                };
                var encoderDesc = new WGPUCommandEncoderDescriptor { };
                var encoder2 = WGPU.wgpuDeviceCreateCommandEncoder(Device, &encoderDesc);
                WGPU.wgpuCommandEncoderCopyTextureToBuffer(encoder2,
                     &source,
                     &destination,
                     &copySize);
                var desc2 = new WGPUCommandBufferDescriptor { };
                var cmdBuffer = WGPU.wgpuCommandEncoderFinish(encoder2, &desc2);
                WGPU.wgpuQueueSubmit(queue, 1, &cmdBuffer);
            }

            var wd = (WorkDoneData*)Marshal.AllocHGlobal(sizeof(WorkDoneData));
            *wd = new WorkDoneData
            {
                Buffer = PixelBuffer,
                BufferSize = (int)BufferSize,
                Width = Width,
                Height = Height
            };

            WGPU.wgpuQueueOnSubmittedWorkDone(queue, &QueueWorkDone, wd);

        }

        WGPU.wgpuSurfacePresent(Surface);
        WGPU.wgpuCommandBufferRelease(commandBuffer);
        WGPU.wgpuRenderPassEncoderRelease(renderPass);
        WGPU.wgpuCommandEncoderRelease(encoder);
        WGPU.wgpuTextureViewRelease(view);
        WGPU.wgpuTextureRelease(surfaceTexture.texture);
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


        Window.Render += (t) =>
        {
            if (stoppingToken.IsCancellationRequested)
            {
                Window.Invoke(() =>
                {
                    Window.Close();
                });
            }
            //Render();
            WindowOnRender(t);
        };

        Window.Run();
    }
}

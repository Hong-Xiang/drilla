using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using Silk.NET.Windowing;
namespace DualDrill.Engine;

public unsafe sealed class WebGPUWindowService : BackgroundService
{

    Instance* Instance { get; set; }
    Adapter* Adapter { get; set; }
    Device* Device { get; set; }

    ShaderModule* Shader { get; set; }
    RenderPipeline* Pipeline { get; set; }

    Texture* TargetTexture { get; set; }
    TextureView* TextureView { get; set; }

    Silk.NET.WebGPU.Buffer* PixelBuffer { get; set; }

    readonly int Width = 800;
    readonly int Height = 600;
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

    public WebGPU wgpu { get; } = new WebGPU(WebGPU.CreateDefaultContext(new string[] { "wgpu_native.dll" }));

    //WebGPU.GetApi();
    IWindow Window { get; set; }
    Surface* Surface { get; set; }

    TextureFormat TextureFormat = TextureFormat.Bgra8Unorm;

    public WebGPUWindowService()
    {
    }

    void Init()
    {


        Surface = Window.CreateWebGPUSurface(wgpu, Instance);


        var requestAdapterOption = new RequestAdapterOptions
        {
            CompatibleSurface = Surface,
            PowerPreference = PowerPreference.HighPerformance
        };

        wgpu.InstanceRequestAdapter(Instance,
            in requestAdapterOption,
            new PfnRequestAdapterCallback((status, adapter, message, _) =>
            {
                if (status == RequestAdapterStatus.Success)
                {
                    Adapter = adapter;
                }
                else
                {
                    Console.WriteLine(Marshal.PtrToStringUTF8((nint)message));
                }
            }), null);
        if (Adapter is null)
        {
            throw new NullReferenceException("Failed to request adapter");
        }

        TextureFormat = wgpu.SurfaceGetPreferredFormat(Surface, Adapter);

        PrintAdapterFeatures();

        {
            var desc = new DeviceDescriptor
            {
            };
            wgpu.AdapterRequestDevice(Adapter, in desc,
            new PfnRequestDeviceCallback((status, device, message, payload) =>
            {
                if (status == RequestDeviceStatus.Success)
                {
                    Device = device;
                }
                else
                {
                    Console.WriteLine($"Failed to get divice, message: {SilkMarshal.PtrToString((IntPtr)message)}");
                }

            }), null);
            Console.WriteLine($"Got device {(nuint)Device:X}");

            wgpu.DeviceSetUncapturedErrorCallback(Device,
                new PfnErrorCallback(
                static (errorType, message, payload) =>
                {
                    Console.WriteLine($"{errorType}: {SilkMarshal.PtrToString((nint)message)}");
                }), default);
        }

        { //Load shader
            var wgslDescriptor = new ShaderModuleWGSLDescriptor
            {
                Code = (byte*)SilkMarshal.StringToPtr(SHADER),
                Chain = new ChainedStruct
                {
                    SType = SType.ShaderModuleWgslDescriptor
                }
            };

            var shaderModuleDescriptor = new ShaderModuleDescriptor
            {
                NextInChain = (ChainedStruct*)(&wgslDescriptor),
            };

            Shader = wgpu.DeviceCreateShaderModule(Device, in shaderModuleDescriptor);

            Console.WriteLine($"Created shader {(nuint)Shader:X}");
        } //Load shader

        { //Create pipeline

            var blendState = new BlendState
            {
                Color = new BlendComponent
                {
                    SrcFactor = BlendFactor.One,
                    DstFactor = BlendFactor.Zero,
                    Operation = BlendOperation.Add
                },
                Alpha = new BlendComponent
                {
                    SrcFactor = BlendFactor.One,
                    DstFactor = BlendFactor.Zero,
                    Operation = BlendOperation.Add
                }
            };

            var colorTargetState = new ColorTargetState
            {
                Format = TextureFormat,
                Blend = &blendState,
                WriteMask = ColorWriteMask.All
            };

            var fragmentState = new FragmentState
            {
                Module = Shader,
                TargetCount = 1,
                Targets = &colorTargetState,
                EntryPoint = (byte*)SilkMarshal.StringToPtr("fs_main")
            };

            var renderPipelineDescriptor = new RenderPipelineDescriptor
            {
                Vertex = new VertexState
                {
                    Module = Shader,
                    EntryPoint = (byte*)SilkMarshal.StringToPtr("vs_main"),
                },
                Primitive = new PrimitiveState
                {
                    Topology = PrimitiveTopology.TriangleList,
                    StripIndexFormat = IndexFormat.Undefined,
                    FrontFace = FrontFace.Ccw,
                    CullMode = CullMode.None
                },
                Multisample = new MultisampleState
                {
                    Count = 1,
                    Mask = ~0u,
                    AlphaToCoverageEnabled = false
                },
                Fragment = &fragmentState,
                DepthStencil = null
            };

            Pipeline = wgpu.DeviceCreateRenderPipeline(Device, in renderPipelineDescriptor);

            Console.WriteLine($"Created pipeline {(nuint)Pipeline:X}");
        } //Create pipeline

        { // Create texture
            var targetTextureDescriptor = new TextureDescriptor
            {
                Label = (byte*)SilkMarshal.StringToPtr("Render Target"),
                Dimension = TextureDimension.Dimension2D,
                Size = new((uint)Width, (uint)Height, 1),
                Format = TextureFormat,
                MipLevelCount = 1,
                SampleCount = 1,
                Usage = TextureUsage.RenderAttachment | TextureUsage.CopySrc,
                ViewFormatCount = 0,
            };
            TargetTexture = wgpu.DeviceCreateTexture(Device, in targetTextureDescriptor);
        }

        {
            var desc = new TextureViewDescriptor
            {
                Aspect = TextureAspect.All,
                BaseArrayLayer = 0,
                ArrayLayerCount = 1,
                BaseMipLevel = 0,
                MipLevelCount = 1,
                Dimension = TextureViewDimension.Dimension2D,
                Format = TextureFormat
            };

            TextureView = wgpu.TextureCreateView(TargetTexture, in desc);
        }

        {
            var desc = new BufferDescriptor
            {
                MappedAtCreation = false,
                Usage = BufferUsage.MapRead | BufferUsage.CopyDst,
                Size = (uint)(4 * Width * Height),
            };

            PixelBuffer = wgpu.DeviceCreateBuffer(Device, in desc);
        }
        CreateSwapChain();
    }
    void CreateSwapChain()
    {
        var config = new SurfaceConfiguration
        {
            Usage = TextureUsage.RenderAttachment,
            Format = TextureFormat,
            PresentMode = PresentMode.Fifo,
            Device = Device,
            Width = (uint)Window.FramebufferSize.X,
            Height = (uint)Window.FramebufferSize.Y
        };
        wgpu.SurfaceConfigure(Surface, in config);
    }
    private static void DeviceLost(DeviceLostReason arg0, byte* arg1, void* arg2)
    {
        Console.WriteLine($"Device lost! Reason: {arg0} Message: {SilkMarshal.PtrToString((nint)arg1)}");
    }


    private static void UncapturedError(ErrorType arg0, byte* arg1, void* arg2)
    {
        Console.WriteLine($"{arg0}: {SilkMarshal.PtrToString((nint)arg1)}");
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
            var instanceDescriptor = new InstanceDescriptor();
            Instance = wgpu.CreateInstance(in instanceDescriptor);
            if (Instance is null)
            {
                throw new Exception("Failed to create instance");
            }
        }

        Surface = Window.CreateWebGPUSurface(wgpu, Instance);


        { //Get adapter
            var requestAdapterOptions = new RequestAdapterOptions
            {
                CompatibleSurface = Surface,
                BackendType = BackendType.WebGpu,
                PowerPreference = PowerPreference.HighPerformance
            };

            wgpu.InstanceRequestAdapter
            (
                Instance,
                in requestAdapterOptions,
                new PfnRequestAdapterCallback((status, adapter, _, _) =>
                {
                    if (status == RequestAdapterStatus.Success)
                    {
                        Adapter = adapter;
                    }
                }),
                null
            );

            Console.WriteLine($"Got adapter {(nuint)Adapter:X}");
        } //Get adapter

        if (Adapter is null)
        {
            throw new NullReferenceException("Failed to get adaptor");
        }

        TextureFormat = wgpu.SurfaceGetPreferredFormat(Surface, Adapter);

        PrintAdapterFeatures();

        { //Get device
            var deviceDescriptor = new DeviceDescriptor
            {
                DeviceLostCallback = new PfnDeviceLostCallback(DeviceLost),
            };

            wgpu.AdapterRequestDevice
            (
                Adapter,
                in deviceDescriptor,
                new PfnRequestDeviceCallback((status, device1, _, _) =>
                {
                    if (status == RequestDeviceStatus.Success)
                    {
                        Device = device1;
                    }
                }),
                null
            );

            Console.WriteLine($"Got device {(nuint)Device:X}");
        } //Get device

        wgpu.DeviceSetUncapturedErrorCallback(Device, new PfnErrorCallback(UncapturedError), null);

        { //Load shader
            var wgslDescriptor = new ShaderModuleWGSLDescriptor
            {
                Code = (byte*)SilkMarshal.StringToPtr(SHADER),
                Chain = new ChainedStruct
                {
                    SType = SType.ShaderModuleWgslDescriptor
                }
            };

            var shaderModuleDescriptor = new ShaderModuleDescriptor
            {
                NextInChain = (ChainedStruct*)(&wgslDescriptor),
            };

            Shader = wgpu.DeviceCreateShaderModule(Device, in shaderModuleDescriptor);

            Console.WriteLine($"Created shader {(nuint)Shader:X}");
        } //Load shader

        var surfaceCapabilities = new SurfaceCapabilities();
        wgpu.SurfaceGetCapabilities(Surface, Adapter, ref surfaceCapabilities);

        { //Create pipeline
            var blendState = new BlendState
            {
                Color = new BlendComponent
                {
                    SrcFactor = BlendFactor.One,
                    DstFactor = BlendFactor.Zero,
                    Operation = BlendOperation.Add
                },
                Alpha = new BlendComponent
                {
                    SrcFactor = BlendFactor.One,
                    DstFactor = BlendFactor.Zero,
                    Operation = BlendOperation.Add
                }
            };

            var colorTargetState = new ColorTargetState
            {
                Format = surfaceCapabilities.Formats[0],
                Blend = &blendState,
                WriteMask = ColorWriteMask.All
            };

            var fragmentState = new FragmentState
            {
                Module = Shader,
                TargetCount = 1,
                Targets = &colorTargetState,
                EntryPoint = (byte*)SilkMarshal.StringToPtr("fs_main")
            };

            var renderPipelineDescriptor = new RenderPipelineDescriptor
            {
                Vertex = new VertexState
                {
                    Module = Shader,
                    EntryPoint = (byte*)SilkMarshal.StringToPtr("vs_main"),
                },
                Primitive = new PrimitiveState
                {
                    Topology = PrimitiveTopology.TriangleList,
                    StripIndexFormat = IndexFormat.Undefined,
                    FrontFace = FrontFace.Ccw,
                    CullMode = CullMode.None
                },
                Multisample = new MultisampleState
                {
                    Count = 1,
                    Mask = ~0u,
                    AlphaToCoverageEnabled = false
                },
                Fragment = &fragmentState,
                DepthStencil = null
            };

            Pipeline = wgpu.DeviceCreateRenderPipeline(Device, in renderPipelineDescriptor);

            Console.WriteLine($"Created pipeline {(nuint)Pipeline:X}");
        } //Create pipeline

        CreateSwapChain();
    }


    private void WindowOnRender(double delta)
    {
        SurfaceTexture surfaceTexture;
        wgpu.SurfaceGetCurrentTexture(Surface, &surfaceTexture);
        switch (surfaceTexture.Status)
        {
            case SurfaceGetCurrentTextureStatus.Timeout:
            case SurfaceGetCurrentTextureStatus.Outdated:
            case SurfaceGetCurrentTextureStatus.Lost:
                // Recreate swapchain,
                wgpu.TextureRelease(surfaceTexture.Texture);
                CreateSwapChain();
                // Skip this frame
                return;
            case SurfaceGetCurrentTextureStatus.OutOfMemory:
            case SurfaceGetCurrentTextureStatus.DeviceLost:
            case SurfaceGetCurrentTextureStatus.Force32:
                throw new Exception($"What is going on bros... {surfaceTexture.Status}");
        }

        var view = wgpu.TextureCreateView(surfaceTexture.Texture, null);

        var commandEncoderDescriptor = new CommandEncoderDescriptor();

        var encoder = wgpu.DeviceCreateCommandEncoder(Device, in commandEncoderDescriptor);

        var colorAttachment = new RenderPassColorAttachment
        {
            View = view,
            ResolveTarget = null,
            LoadOp = LoadOp.Clear,
            StoreOp = StoreOp.Store,
            ClearValue = new()
            {
                R = 0,
                G = 1,
                B = 0,
                A = 1
            }
        };

        var renderPassDescriptor = new RenderPassDescriptor
        {
            ColorAttachments = &colorAttachment,
            ColorAttachmentCount = 1,
            DepthStencilAttachment = null
        };

        var renderPass = wgpu.CommandEncoderBeginRenderPass(encoder, in renderPassDescriptor);

        wgpu.RenderPassEncoderSetPipeline(renderPass, Pipeline);
        wgpu.RenderPassEncoderDraw(renderPass, 3, 1, 0, 0);
        wgpu.RenderPassEncoderEnd(renderPass);

        var queue = wgpu.DeviceGetQueue(Device);
        var desc = new CommandBufferDescriptor();
        var commandBuffer = wgpu.CommandEncoderFinish(encoder, in desc);

        wgpu.QueueSubmit(queue, 1, &commandBuffer);
        wgpu.SurfacePresent(Surface);
        wgpu.CommandBufferRelease(commandBuffer);
        wgpu.RenderPassEncoderRelease(renderPass);
        wgpu.CommandEncoderRelease(encoder);
        wgpu.TextureViewRelease(view);
        wgpu.TextureRelease(surfaceTexture.Texture);
    }

    public void Render()
    {
        SurfaceTexture surfaceTexture = default;
        wgpu.SurfaceGetCurrentTexture(Surface, ref surfaceTexture);
        switch (surfaceTexture.Status)
        {
            case SurfaceGetCurrentTextureStatus.Timeout:
            case SurfaceGetCurrentTextureStatus.Outdated:
            case SurfaceGetCurrentTextureStatus.Lost:
                // Recreate swapchain,
                wgpu.TextureRelease(surfaceTexture.Texture);
                CreateSwapChain();
                // Skip this frame
                return;
            case SurfaceGetCurrentTextureStatus.OutOfMemory:
            case SurfaceGetCurrentTextureStatus.DeviceLost:
            case SurfaceGetCurrentTextureStatus.Force32:
                throw new Exception($"What is going on bros... {surfaceTexture.Status}");


        }

        var view = wgpu.TextureCreateView(surfaceTexture.Texture, null);

        var commandEncoderDescriptor = new CommandEncoderDescriptor();

        var encoder = wgpu.DeviceCreateCommandEncoder(Device, in commandEncoderDescriptor);

        Console.WriteLine($"Texture view pointer: {(nint)TextureView}");
        var colorAttachment = new RenderPassColorAttachment
        {
            View = view,
            ResolveTarget = null,
            LoadOp = LoadOp.Clear,
            StoreOp = StoreOp.Store,
            ClearValue = new()
            {
                R = 0,
                G = 1,
                B = 0,
                A = 1
            }
        };

        var renderPassDescriptor = new RenderPassDescriptor
        {
            ColorAttachments = &colorAttachment,
            ColorAttachmentCount = 1,
            DepthStencilAttachment = null
        };

        var renderPass = wgpu.CommandEncoderBeginRenderPass(encoder, in renderPassDescriptor);

        wgpu.RenderPassEncoderSetPipeline(renderPass, Pipeline);
        wgpu.RenderPassEncoderDraw(renderPass, 3, 1, 0, 0);
        wgpu.RenderPassEncoderEnd(renderPass);

        var queue = wgpu.DeviceGetQueue(Device);

        var commandBufferDescriptor = new CommandBufferDescriptor();
        var commandBuffer = wgpu.CommandEncoderFinish(encoder, in commandBufferDescriptor);
        wgpu.QueueSubmit(queue, 1, &commandBuffer);

        wgpu.SurfacePresent(Surface);
    }

    private void SaveTextureToImage()
    {
        var waitEvent = new AutoResetEvent(false);

        wgpu.BufferMapAsync(PixelBuffer, MapMode.Read, 0, BufferSize, new PfnBufferMapCallback((status, data) =>
        {
            waitEvent.Set();
        }), default);

        waitEvent.WaitOne();
        var data = new Span<byte>(wgpu.BufferGetConstMappedRange(PixelBuffer, 0, BufferSize), (int)BufferSize);

        var image = SixLabors.ImageSharp.Image.LoadPixelData<SixLabors.ImageSharp.PixelFormats.Rgba32>(data, Width, Height);
        image.SaveAsPng("./output.png");
        wgpu.BufferUnmap(PixelBuffer);
    }

    private unsafe void PrintAdapterFeatures()
    {
        var count = (int)wgpu.AdapterEnumerateFeatures(Adapter, null);

        var features = stackalloc FeatureName[count];

        wgpu.AdapterEnumerateFeatures(Adapter, features);

        Console.WriteLine("Adapter features:");

        for (var i = 0; i < count; i++)
        {
            Console.WriteLine($"\t{features[i]}");
        }
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
            Console.WriteLine(t);
        };

        Window.Run();
    }
}

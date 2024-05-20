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
using DualDrill.Graphics;
namespace DualDrill.Engine;

public unsafe sealed class WebGPUWindowService : BackgroundService
{

    Graphics.Instance Instance { get; set; }
    Graphics.Adapter Adapter { get; set; }
    Graphics.Device Device { get; set; }

    Graphics.ShaderModule Shader { get; set; }
    RenderPipeline* Pipeline { get; set; }

    Texture* TargetTexture { get; set; }
    TextureView* TextureView { get; set; }

    Graphics.Buffer PixelBuffer { get; set; }

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

    public Silk.NET.WebGPU.WebGPU wgpu { get; } = Silk.NET.WebGPU.WebGPU.GetApi();
    //WebGPU(WebGPU.CreateDefaultContext(new string[] { "wgpu_native.dll" }));

    //WebGPU.GetApi();
    IWindow Window { get; set; }
    Surface* Surface { get; set; }

    TextureFormat TextureFormat = TextureFormat.Bgra8UnormSrgb;

    public WebGPUWindowService()
    {
    }
    void CreateSwapChain()
    {
        var config = new SurfaceConfiguration
        {
            Usage = TextureUsage.RenderAttachment | TextureUsage.CopySrc,
            Format = TextureFormat,
            PresentMode = PresentMode.Fifo,
            Device = Device.Handle,
            Width = (uint)Window.FramebufferSize.X,
            Height = (uint)Window.FramebufferSize.Y,
            AlphaMode = CompositeAlphaMode.Opaque,
        };
        wgpu.SurfaceConfigure(Surface, in config);
    }

    private static void DeviceLost(DeviceLostReason arg0, byte* arg1, void* arg2)
    {
        Console.WriteLine($"Device lost! Reason: {arg0} Message: {SilkMarshal.PtrToString((nint)arg1)}");
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
            Instance = Graphics.Instance.Create(new());
            if (Instance is null)
            {
                throw new Exception("Failed to create instance");
            }
        }

        Surface = Window.CreateWebGPUSurface(wgpu, Instance.Handle);


        { //Get adapter
            var requestAdapterOptions = new RequestAdapterOptions
            {
                CompatibleSurface = Surface,
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
            Adapter = Instance.RequestAdapter(new() { CompatibleSurface = Surface });
            Adapter.PrintInfo();
        } //Get adapter



        TextureFormat = wgpu.SurfaceGetPreferredFormat(Surface, Adapter.Handle);

        Adapter.PrintFeatures();

        { //Get device
            Device = Adapter.RequestDevice(new()
            {
                DeviceLostCallback = new PfnDeviceLostCallback(DeviceLost),
            });
            Console.WriteLine($"Got device {(nuint)Device.Handle:X}");
        } //Get device


        { //Load shader
            //var wgslDescriptor = new ShaderModuleWGSLDescriptor
            //{
            //    Code = (byte*)SilkMarshal.StringToPtr(SHADER),
            //    Chain = new ChainedStruct
            //    {
            //        SType = SType.ShaderModuleWgslDescriptor
            //    }
            //};

            //var shaderModuleDescriptor = new ShaderModuleDescriptor
            //{
            //    NextInChain = (ChainedStruct*)(&wgslDescriptor),
            //};

            //Shader = wgpu.DeviceCreateShaderModule(Device.Handle, in shaderModuleDescriptor);

            Shader = Device.CreateShaderModule(SHADER);

            Console.WriteLine($"Created shader {(nuint)Shader.Handle:X}");
        } //Load shader

        var surfaceCapabilities = new SurfaceCapabilities();
        wgpu.SurfaceGetCapabilities(Surface, Adapter.Handle, ref surfaceCapabilities);

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
                //Blend = &blendState,
                WriteMask = ColorWriteMask.All
            };

            var fragmentState = new FragmentState
            {
                Module = Shader.Handle,
                TargetCount = 1,
                Targets = &colorTargetState,
                EntryPoint = (byte*)SilkMarshal.StringToPtr("fs_main")
            };

            var renderPipelineDescriptor = new RenderPipelineDescriptor
            {
                Vertex = new VertexState
                {
                    Module = Shader.Handle,
                    EntryPoint = (byte*)SilkMarshal.StringToPtr("vs_main"),
                },
                Primitive = new PrimitiveState
                {
                    Topology = PrimitiveTopology.TriangleList,
                    //StripIndexFormat = IndexFormat.Undefined,
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

            Pipeline = Device.CreateRenderPipeline(in renderPipelineDescriptor);

            Console.WriteLine($"Created pipeline {(nuint)Pipeline:X}");
        } //Create pipeline

        PixelBuffer = Device.CreateBuffer(new BufferDescriptor
        {
            MappedAtCreation = false,
            Usage = BufferUsage.MapRead | BufferUsage.CopyDst,
            Size = (uint)(4 * Width * Height),
        });

        CreateSwapChain();
    }

    bool IsFirstRender = true;

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

        var encoder = wgpu.DeviceCreateCommandEncoder(Device.Handle, in commandEncoderDescriptor);


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

        var queue = wgpu.DeviceGetQueue(Device.Handle);
        var desc = new CommandBufferDescriptor();
        var commandBuffer = wgpu.CommandEncoderFinish(encoder, in desc);
        wgpu.QueueSubmit(queue, 1, &commandBuffer);




        if (IsFirstRender)
        {
            IsFirstRender = false;

            {
                var source = new ImageCopyTexture
                {
                    Texture = surfaceTexture.Texture,
                };
                var destination = new ImageCopyBuffer
                {
                    Buffer = PixelBuffer.Ptr,
                    Layout = new TextureDataLayout
                    {
                        BytesPerRow = (uint)(4 * Width),
                        Offset = 0,
                        RowsPerImage = (uint)Height
                    }
                };
                var copySize = new Extent3D
                {
                    Width = (uint)Width,
                    Height = (uint)Height,
                    DepthOrArrayLayers = 1
                };
                var encoderDesc = new CommandEncoderDescriptor { };
                var encoder2 = wgpu.DeviceCreateCommandEncoder(Device.Handle, in encoderDesc);
                wgpu.CommandEncoderCopyTextureToBuffer(encoder2,
                    source,
                    destination,
                    in copySize);
                var desc2 = new CommandBufferDescriptor { };
                var cmdBuffer = wgpu.CommandEncoderFinish(encoder2, in desc2);
                wgpu.QueueSubmit(queue, 1, &cmdBuffer);
            }

            wgpu.QueueOnSubmittedWorkDone(queue, new PfnQueueWorkDoneCallback((status, data) =>
                    {
                        wgpu.BufferMapAsync(PixelBuffer.Ptr, MapMode.Read, 0, BufferSize, new PfnBufferMapCallback((status, data) =>
                        {
                            if (status == BufferMapAsyncStatus.Success)
                            {
                                Console.WriteLine("Map succeed");
                                var byteData = new Span<byte>(wgpu.BufferGetConstMappedRange(PixelBuffer.Ptr, 0, BufferSize), (int)BufferSize);
                                var image = Image.LoadPixelData<SixLabors.ImageSharp.PixelFormats.Bgra32>(byteData, Width, Height);
                                image.SaveAsPng("./output.png");
                                PixelBuffer.Unmap();
                            }
                            else
                            {
                                Console.WriteLine($"Map failed with status ${Enum.GetName(status)}");
                            }
                            //waitEvent.Set();
                        }), default);
                    }), null);

        }

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

        var encoder = wgpu.DeviceCreateCommandEncoder(Device.Handle, in commandEncoderDescriptor);

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

        var queue = wgpu.DeviceGetQueue(Device.Handle);

        var commandBufferDescriptor = new CommandBufferDescriptor();
        var commandBuffer = wgpu.CommandEncoderFinish(encoder, in commandBufferDescriptor);
        wgpu.QueueSubmit(queue, 1, &commandBuffer);

        wgpu.SurfacePresent(Surface);
    }

    private void SaveTextureToImage()
    {
        var waitEvent = new AutoResetEvent(false);

        wgpu.BufferMapAsync(PixelBuffer.Ptr, MapMode.Read, 0, BufferSize, new PfnBufferMapCallback((status, data) =>
        {
            waitEvent.Set();
        }), default);

        waitEvent.WaitOne();
        var data = new Span<byte>(wgpu.BufferGetConstMappedRange(PixelBuffer.Ptr, 0, BufferSize), (int)BufferSize);

        var image = SixLabors.ImageSharp.Image.LoadPixelData<SixLabors.ImageSharp.PixelFormats.Rgba32>(data, Width, Height);
        image.SaveAsPng("./output.png");
        wgpu.BufferUnmap(PixelBuffer.Ptr);
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

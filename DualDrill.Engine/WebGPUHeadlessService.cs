using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp;

namespace DualDrill.Engine;

public unsafe sealed class WebGPUHeadlessService
{

    Instance* Instance { get; }
    Adapter* Adapter { get; set; }
    Device* Device { get; set; }

    ShaderModule* ShaderModule { get; set; }
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

    public WebGPU WebGPU { get; } = WebGPU.GetApi();
    public WebGPUHeadlessService()
    {
        {
            var desc = new InstanceDescriptor();
            Instance = WebGPU.CreateInstance(ref desc);
        }

        var requestAdapterOption = new RequestAdapterOptions
        {
            CompatibleSurface = null,
            PowerPreference = PowerPreference.HighPerformance
        };

        WebGPU.InstanceRequestAdapter(Instance,
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

        PrintAdapterFeatures();

        WebGPU.AdapterRequestDevice(Adapter, new DeviceDescriptor
        {
            DefaultQueue = new QueueDescriptor()
        },
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

        WebGPU.DeviceSetUncapturedErrorCallback(Device,
            new PfnErrorCallback(
            static (errorType, message, payload) =>
            {
                Console.WriteLine($"{errorType}: {SilkMarshal.PtrToString((nint)message)}");
            }), default);

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

            ShaderModule = WebGPU.DeviceCreateShaderModule(Device, in shaderModuleDescriptor);

            Console.WriteLine($"Created shader {(nuint)ShaderModule:X}");
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
                Format = TextureFormat.Rgba8UnormSrgb,
                Blend = &blendState,
                WriteMask = ColorWriteMask.All
            };

            var fragmentState = new FragmentState
            {
                Module = ShaderModule,
                TargetCount = 1,
                Targets = &colorTargetState,
                EntryPoint = (byte*)SilkMarshal.StringToPtr("fs_main")
            };

            var renderPipelineDescriptor = new RenderPipelineDescriptor
            {
                Vertex = new VertexState
                {
                    Module = ShaderModule,
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

            Pipeline = WebGPU.DeviceCreateRenderPipeline(Device, in renderPipelineDescriptor);

            Console.WriteLine($"Created pipeline {(nuint)Pipeline:X}");
        } //Create pipeline

        var format = TextureFormat.Rgba8UnormSrgb;
        { // Create texture
            var targetTextureDescriptor = new TextureDescriptor
            {
                Label = (byte*)SilkMarshal.StringToPtr("Render Target"),
                Dimension = TextureDimension.Dimension2D,
                Size = new((uint)Width, (uint)Height, 1),
                Format = format,
                MipLevelCount = 1,
                SampleCount = 1,
                Usage = TextureUsage.RenderAttachment | TextureUsage.CopySrc,
                ViewFormatCount = 0,
            };
            TargetTexture = WebGPU.DeviceCreateTexture(Device, in targetTextureDescriptor);
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
                Format = format
            };

            TextureView = WebGPU.TextureCreateView(TargetTexture, in desc);
        }

        {
            var desc = new BufferDescriptor
            {
                MappedAtCreation = false,
                Usage = BufferUsage.MapRead | BufferUsage.CopyDst,
                Size = (uint)(4 * Width * Height),
            };

            PixelBuffer = WebGPU.DeviceCreateBuffer(Device, in desc);
        }
    }

    public void Render()
    {
        var commandEncoderDescriptor = new CommandEncoderDescriptor();

        var encoder = WebGPU.DeviceCreateCommandEncoder(Device, in commandEncoderDescriptor);

        Console.WriteLine($"Texture view pointer: {(nint)TextureView}");
        var colorAttachment = new RenderPassColorAttachment
        {
            View = TextureView,
            ResolveTarget = null,
            LoadOp = LoadOp.Clear,
            StoreOp = StoreOp.Store,
            ClearValue = new Silk.NET.WebGPU.Color
            {
                R = 1,
                G = 1,
                B = 1,
                A = 1
            }
        };

        var renderPassDescriptor = new RenderPassDescriptor
        {
            ColorAttachments = &colorAttachment,
            ColorAttachmentCount = 1,
            DepthStencilAttachment = null
        };

        var renderPass = WebGPU.CommandEncoderBeginRenderPass(encoder, in renderPassDescriptor);

        WebGPU.RenderPassEncoderSetPipeline(renderPass, Pipeline);
        WebGPU.RenderPassEncoderDraw(renderPass, 3, 1, 0, 0);
        WebGPU.RenderPassEncoderEnd(renderPass);

        var queue = WebGPU.DeviceGetQueue(Device);

        var commandBufferDescriptor = new CommandBufferDescriptor();
        var commandBuffer = WebGPU.CommandEncoderFinish(encoder, in commandBufferDescriptor);
        WebGPU.QueueSubmit(queue, 1, &commandBuffer);

        //var waitEvent = new AutoResetEvent(false);

        //WebGPU.BufferMapAsync(PixelBuffer, MapMode.Read, 0, BufferSize, new PfnBufferMapCallback((status, data) =>
        //{
        //    waitEvent.Set();
        //}), default);

        //waitEvent.WaitOne();
        //var data = new Span<byte>(WebGPU.BufferGetConstMappedRange(PixelBuffer, 0, BufferSize), (int)BufferSize);

        //var image = Image.LoadPixelData<SixLabors.ImageSharp.PixelFormats.Rgba32>(data, Width, Height);
        //image.SaveAsPng("./output.png");
        //WebGPU.BufferUnmap(PixelBuffer);

    }

    private unsafe void PrintAdapterFeatures()
    {
        var count = (int)WebGPU.AdapterEnumerateFeatures(Adapter, null);

        var features = stackalloc FeatureName[count];

        WebGPU.AdapterEnumerateFeatures(Adapter, features);

        Console.WriteLine("Adapter features:");

        for (var i = 0; i < count; i++)
        {
            Console.WriteLine($"\t{features[i]}");
        }
    }
}

using Silk.NET.WebGPU;
using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using Silk.NET.Core.Native;

namespace DualDrill.Engine;

public unsafe sealed class SimpleTriangleRendererLegacy
{
    Graphics.Instance Instance { get; }
    Graphics.Adapter Adapter { get; }
    Graphics.Device Device { get; }

    Graphics.ShaderModule ShaderModule { get; }
    RenderPipeline* Pipeline { get; }

    Texture* TargetTexture { get; }
    TextureView* TextureView { get; }
    TextureFormat TextureFormat = TextureFormat.Bgra8UnormSrgb;
    Graphics.Buffer PixelBuffer { get; }

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
    private static void DeviceLost(DeviceLostReason arg0, byte* arg1, void* arg2)
    {
        Console.WriteLine($"Device lost! Reason: {arg0} Message: {SilkMarshal.PtrToString((nint)arg1)}");
    }

    public WebGPU WebGPU { get; } = WebGPU.GetApi();
    public SimpleTriangleRendererLegacy()
    {
        Instance = Graphics.Instance.Create(new());
        Adapter = Instance.RequestAdapter(new()
        {
            PowerPreference = PowerPreference.HighPerformance
        });
        Adapter.PrintInfo();
        Adapter.PrintFeatures();

        Device = Adapter.RequestDevice(new()
        {
            DeviceLostCallback = new PfnDeviceLostCallback(DeviceLost),
        });
        Console.WriteLine($"Got device {(nuint)Device.Handle:X}");

        ShaderModule = Device.CreateShaderModule(SHADER);

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
                Module = ShaderModule.Handle,
                TargetCount = 1,
                Targets = &colorTargetState,
                EntryPoint = (byte*)SilkMarshal.StringToPtr("fs_main")
            };

            var renderPipelineDescriptor = new RenderPipelineDescriptor
            {
                Vertex = new VertexState
                {
                    Module = ShaderModule.Handle,
                    EntryPoint = (byte*)SilkMarshal.StringToPtr("vs_main"),
                },
                Primitive = new PrimitiveState
                {
                    Topology = PrimitiveTopology.TriangleList,
                    //StripIndexFormat = IndexFormat.Undefined,
                    //FrontFace = FrontFace.Ccw,
                    //CullMode = CullMode.None
                },
                Multisample = new MultisampleState
                {
                    Count = 1,
                    Mask = ~0u,
                    //AlphaToCoverageEnabled = false
                },
                Fragment = &fragmentState,
                DepthStencil = null
            };

            Pipeline = WebGPU.DeviceCreateRenderPipeline(Device.Handle, in renderPipelineDescriptor);

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
            TargetTexture = WebGPU.DeviceCreateTexture(Device.Handle, in targetTextureDescriptor);
            SilkMarshal.Free((nint)targetTextureDescriptor.Label);
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

            TextureView = WebGPU.TextureCreateView(TargetTexture, in desc);
        }

        PixelBuffer = Device.CreateBuffer(new()
        {
            MappedAtCreation = false,
            Usage = BufferUsage.MapRead | BufferUsage.CopyDst,
            Size = (uint)(4 * Width * Height),
        });
    }

    public Image Render()
    {
        Console.WriteLine("render called");
        var queue = WebGPU.DeviceGetQueue(Device.Handle);
        {

            var commandEncoderDescriptor = new CommandEncoderDescriptor();

            var encoder = WebGPU.DeviceCreateCommandEncoder(Device.Handle, in commandEncoderDescriptor);

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


            var commandBufferDescriptor = new CommandBufferDescriptor();
            var commandBuffer = WebGPU.CommandEncoderFinish(encoder, in commandBufferDescriptor);

            WebGPU.QueueSubmit(queue, 1, &commandBuffer);
        }
        WebGPU.QueueOnSubmittedWorkDone(queue, new PfnQueueWorkDoneCallback((status, data) =>
        {
            Console.WriteLine("queued work done 1");
        }), null);
        {
            var source = new ImageCopyTexture
            {
                Texture = TargetTexture,
            };
            var bytesPerRow = (uint)(4 * Width);
            var alignedBytesPerRow = (bytesPerRow + 255) & ~255;
            var destination = new ImageCopyBuffer
            {
                Buffer = PixelBuffer.Ptr,
                Layout = new TextureDataLayout
                {
                    BytesPerRow = (uint)alignedBytesPerRow,
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
            var encoder2 = WebGPU.DeviceCreateCommandEncoder(Device.Handle, in encoderDesc);
            WebGPU.CommandEncoderCopyTextureToBuffer(encoder2,
                in source,
                in destination,
                in copySize);
            var desc2 = new CommandBufferDescriptor { };
            var cmdBuffer = WebGPU.CommandEncoderFinish(encoder2, in desc2);
            WebGPU.QueueSubmit(queue, 1, &cmdBuffer);
        }

        Console.WriteLine("all cmd submitted");
        Image<SixLabors.ImageSharp.PixelFormats.Bgra32>? result = null;
        WebGPU.QueueOnSubmittedWorkDone(queue, new PfnQueueWorkDoneCallback((status, data) =>
        {
            Console.WriteLine("queued work done 2");
            //var resultTCS = new TaskCompletionSource<Image>();
            WebGPU.BufferMapAsync(PixelBuffer.Ptr, MapMode.Read, 0, BufferSize, new PfnBufferMapCallback((status, data) =>
    {
        if (status == BufferMapAsyncStatus.Success)
        {
            Console.WriteLine("Map succeed");
            var byteData = new Span<byte>(WebGPU.BufferGetConstMappedRange(PixelBuffer.Ptr, 0, BufferSize), (int)BufferSize);
            var image = Image.LoadPixelData<SixLabors.ImageSharp.PixelFormats.Bgra32>(byteData, Width, Height);
            image.SaveAsPng("./output-headless-2.png");
            result = image;
            //resultTCS.SetResult(image);
            WebGPU.BufferUnmap(PixelBuffer.Ptr);
        }
        else
        {
            Console.WriteLine($"Map failed with status ${Enum.GetName(status)}");
        }
        //waitEvent.Set();
    }), default);
        }), null);



        //var waitEvent = new AutoResetEvent(false);

        //return resultTCS.Task;
        return result!;
    }


}

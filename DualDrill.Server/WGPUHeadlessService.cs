﻿using DualDrill.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Buffers;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace DualDrill.Server;

public sealed class WGPUHeadlessService : IDisposable
{
    WGPUProviderService WGPUProviderService { get; }
    GPUDevice Device => WGPUProviderService.Device;
    GPUQueue Queue => WGPUProviderService.Queue;

    //Graphics.ShaderModule Shader { get; set; }
    GPUShaderModule ShaderModule { get; set; }
    GPUPipelineLayout PipelineLayout { get; set; }
    GPURenderPipeline Pipeline { get; set; }

    GPUBuffer PixelBuffer { get; set; }
    GPUTexture RenderTarget { get; set; }
    GPUTextureView RenderTargetView { get; set; }

    public ArrayPool<byte> ResultBufferPool { get; }

    public readonly int Width = 1472;
    public readonly int Height = 936 * 2;

    uint BufferSize => (uint)(Width * Height * 4);

    public readonly GPUTextureFormat TextureFormat = GPUTextureFormat.BGRA8UnormSrgb;

    readonly record struct RenderRequest(
        double Time,
        TaskCompletionSource<Image<Bgra32>> ResultCompletionSource
    )
    {
    }

    readonly Channel<int> Ready = Channel.CreateBounded<int>(1);

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


    public WGPUHeadlessService(WGPUProviderService wgpuProvider)
    {
        WGPUProviderService = wgpuProvider;
        ShaderModule = Device.CreateShaderModule(SHADER);
        PipelineLayout = Device.CreatePipelineLayout(new GPUPipelineLayoutDescriptor());
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

        Ready.Writer.TryWrite(0);

        ResultBufferPool = ArrayPool<byte>.Shared;
    }

    public async ValueTask Render(double time, GPUTexture renderTarget)
    {
        //await Ready.Reader.ReadAsync().ConfigureAwait(false);
        await RenderInternal(time, renderTarget).ConfigureAwait(false);
        //await Ready.Writer.WriteAsync(0).ConfigureAwait(false);
    }
    async ValueTask RenderInternal(double time, GPUTexture renderTarget)
    {
        //var texture = RenderTarget;
        using var view = renderTarget.CreateView();
        using var encoder = Device.CreateCommandEncoder(new());

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

        rp.SetPipeline(Pipeline);
        rp.Draw(3, 1, 0, 0);
        rp.End();

        using var drawCommands = encoder.Finish(new());
        Queue.Submit([drawCommands]);




        //{
        //    using var e = Device.CreateCommandEncoder(new());
        //    e.CopyTextureToBuffer(new GPUImageCopyTexture
        //    {
        //        Texture = renderTarget
        //    }, new GPUImageCopyBuffer
        //    {
        //        Buffer = PixelBuffer,
        //        Layout = new GPUTextureDataLayout
        //        {
        //            BytesPerRow = 4 * Width,
        //            Offset = 0,
        //            RowsPerImage = Height
        //        }
        //    }, new GPUExtent3D
        //    {
        //        Width = Width,
        //        Height = Height,
        //        DepthOrArrayLayers = 1
        //    });

        //    using var cb = e.Finish(new());
        //    Queue.Submit([cb]);
        //}
        //await Queue.WaitSubmittedWorkDoneAsync().ConfigureAwait(false);

        //{
        //    using var _ = await PixelBuffer.MapAsync(GPUMapMode.Read, 0, (int)BufferSize).ConfigureAwait(true);
        //    //Image<Bgra32> ReadImage()
        //    //{
        //    //    var byteData = PixelBuffer.GetConstMappedRange(0, (int)BufferSize);
        //    //    return Image.LoadPixelData<SixLabors.ImageSharp.PixelFormats.Bgra32>(byteData, Width, Height);
        //    //}
        //    //return ReadImage();
        //    byte[] ReadToBuffer()
        //    {
        //        //var buffer = new byte[BufferSize];
        //        //byte[] buffer = null;
        //        //if (BufferQueue.TryDequeue(out var res))
        //        //{
        //        //    buffer = res;
        //        //}
        //        //buffer ??= new byte[(int)BufferSize];
        //        //var buffer = ResultBufferPool.Rent((int)BufferSize);
        //        var buffer = new byte[BufferSize];
        //        var byteData = PixelBuffer.GetConstMappedRange(0, (int)BufferSize);
        //        byteData.CopyTo(buffer);
        //        return buffer;
        //    }
        //    return ReadToBuffer();
        //}
        await Queue.WaitSubmittedWorkDoneAsync().ConfigureAwait(false);
    }



    public void Dispose()
    {
        RenderTargetView.Dispose();
        RenderTarget.Dispose();
        Pipeline.Dispose();
        PipelineLayout.Dispose();
        ShaderModule.Dispose();
    }
}
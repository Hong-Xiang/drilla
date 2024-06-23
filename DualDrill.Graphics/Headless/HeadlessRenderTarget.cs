using System.Buffers;

namespace DualDrill.Graphics.Headless;

public sealed class HeadlessRenderTarget(
    GPUDevice Device,
    int Width,
    int Height,
    GPUTextureFormat Format) : IDisposable
{
    public GPUTexture Texture { get; } = Device.CreateTexture(new GPUTextureDescriptor
    {
        Usage = GPUTextureUsage.RenderAttachment | GPUTextureUsage.CopySrc,
        MipLevelCount = 1,
        SampleCount = 1,
        Dimension = GPUTextureDimension.Dimension2D,
        Size = new()
        {
            Width = Width,
            Height = Height,
            DepthOrArrayLayers = 1
        },
        Format = Format,
    });

    int BufferSizeCPU => 4 * Width * Height;

    static int PaddedBytesPerRow(int width)
    {
        return 4 * width;
    }

    static ulong PaddedBufferSize(int width, int height)
    {
        return (ulong)(PaddedBytesPerRow(width) * height);
    }

    readonly GPUBuffer _bufferGPU = Device.CreateBuffer(new GPUBufferDescriptor
    {
        Usage = GPUBufferUsage.MapRead | GPUBufferUsage.CopyDst,
        Size = PaddedBufferSize(Width, Height)
    });

    readonly IMemoryOwner<byte> _bufferCPU = DotNext.Buffers.UnmanagedMemoryPool<byte>.Shared.Rent(4 * Width * Height);

    public async ValueTask<ReadOnlyMemory<byte>> ReadResultAsync(CancellationToken cancellation)
    {
        using var queue = Device.GetQueue();
        using var e = Device.CreateCommandEncoder(new());
        e.CopyTextureToBuffer(new GPUImageCopyTexture
        {
            Texture = Texture
        }, new GPUImageCopyBuffer
        {
            Buffer = _bufferGPU,
            Layout = new GPUTextureDataLayout
            {
                BytesPerRow = PaddedBytesPerRow(Width),
                Offset = 0,
                RowsPerImage = Height
            }
        }, new GPUExtent3D
        {
            Width = Width,
            Height = Height,
            DepthOrArrayLayers = 1
        });


        using var cb = e.Finish(new());
        queue.Submit([cb]);
        //await queue.WaitSubmittedWorkDoneAsync(cancellation).ConfigureAwait(false);
        var bufferSizeGPU = (int)PaddedBufferSize(Width, Height);
        using var _ = await _bufferGPU.MapAsync(GPUMapMode.Read, 0, bufferSizeGPU, cancellation).ConfigureAwait(false);
        void ReadBytes()
        {
            var gpuData = _bufferGPU.GetConstMappedRange(0, bufferSizeGPU);
            gpuData.CopyTo(_bufferCPU.Memory.Span);
        }
        ReadBytes();
        return _bufferCPU.Memory[..(Width * Height * 4)];
    }

    public void Dispose()
    {
        _bufferCPU.Dispose();
        _bufferGPU.Dispose();
        Texture.Dispose();
    }
}

using System.Buffers;

namespace DualDrill.Graphics.Headless;

public sealed class HeadlessRenderTarget : IDisposable
{
    public HeadlessRenderTarget(GPUDevice device, int width, int height, GPUTextureFormat format)
    {
        Device = device;
        Width = width;
        Height = height;
        Format = format;
        Texture = Device.CreateTexture(new GPUTextureDescriptor
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
        BufferGPU = Device.CreateBuffer(new GPUBufferDescriptor
        {
            Usage = GPUBufferUsage.MapRead | GPUBufferUsage.CopyDst,
            Size = (ulong)GPUBufferByteSize
        });
        BufferCPU = DotNext.Buffers.UnmanagedMemoryPool<byte>.Shared.Rent(CPUBufferByteSize);
    }
    public GPUDevice Device { get; }
    public int Width { get; }
    public int Height { get; }
    public GPUTextureFormat Format { get; }
    public GPUTexture Texture { get; }

    static int PaddedBytesPerRow(int byteSize) => (byteSize + 255) & ~255;
    int PixelByteSize { get; } = 4;
    int CPUBytesPerRow => Width * PixelByteSize;
    int GPUBytesPerRow => PaddedBytesPerRow(CPUBytesPerRow);
    int CPUBufferByteSize => Height * CPUBytesPerRow;
    int GPUBufferByteSize => Height * GPUBytesPerRow;

    GPUBuffer BufferGPU { get; }

    IMemoryOwner<byte> BufferCPU { get; }

    public async ValueTask<ReadOnlyMemory<byte>> ReadResultAsync(CancellationToken cancellation)
    {
        using var queue = Device.GetQueue();
        using var e = Device.CreateCommandEncoder(new());
        e.CopyTextureToBuffer(new GPUImageCopyTexture
        {
            Texture = Texture
        }, new GPUImageCopyBuffer
        {
            Buffer = BufferGPU,
            Layout = new GPUTextureDataLayout
            {
                BytesPerRow = GPUBytesPerRow,
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
        using var _ = await BufferGPU.MapAsync(GPUMapMode.Read, 0, GPUBufferByteSize, cancellation).ConfigureAwait(false);
        void ReadBytes()
        {
            var gpuData = BufferGPU.GetConstMappedRange(0, GPUBufferByteSize);
            for (var i = 0; i < Height; i++)
            {
                var offsetGPU = i * GPUBytesPerRow;
                var offsetCPU = i * CPUBytesPerRow;
                var size = CPUBytesPerRow;
                gpuData[offsetGPU..(offsetGPU + size)].CopyTo(BufferCPU.Memory.Span[offsetCPU..(offsetCPU + size)]);
            }
        }
        ReadBytes();
        return BufferCPU.Memory[..CPUBufferByteSize];
    }

    public void Dispose()
    {
        BufferCPU.Dispose();
        BufferGPU.Dispose();
        Texture.Dispose();
    }
}

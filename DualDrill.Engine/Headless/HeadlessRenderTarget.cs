using DualDrill.Graphics;
using System.Buffers;

namespace DualDrill.Engine.Headless;

public sealed class HeadlessRenderTarget : IDisposable
{
    public HeadlessRenderTarget(
        GPUDevice device,
        int width, int height, GPUTextureFormat format,
        int slotIndex = 0)
    {
        Device = device;
        SlotIndex = slotIndex;
        Width = width;
        Height = height;
        Format = format;
        Texture = Device.CreateTexture(new GPUTextureDescriptor
        {
            Usage = GPUTextureUsage.RenderAttachment | GPUTextureUsage.CopySrc,
            MipLevelCount = 1,
            SampleCount = 1,
            Dimension = GPUTextureDimension._2D,
            Size = new()
            {
                Width = Width,
                Height = Height,
                DepthOrArrayLayers = 1
            },
            Format = Format,
        });
        GPUBuffer = Device.CreateBuffer(new GPUBufferDescriptor
        {
            Usage = GPUBufferUsage.MapRead | GPUBufferUsage.CopyDst,
            Size = (ulong)GPUBufferByteSize
        });
        BufferCPUMemoryOwner = DotNext.Buffers.UnmanagedMemoryPool<byte>.Shared.Rent(CPUBufferByteSize);
    }
    private GPUDevice Device { get; }
    public int SlotIndex { get; }
    public int Width { get; }
    public int Height { get; }
    public GPUTextureFormat Format { get; }
    public GPUTexture Texture { get; }
    public ReadOnlyMemory<byte> Memory => BufferCPUMemoryOwner.Memory[..CPUBufferByteSize];

    static int PaddedBytesPerRow(int byteSize) => byteSize + 255 & ~255;
    int PixelByteSize { get; } = 4;
    int CPUBytesPerRow => Width * PixelByteSize;
    int GPUBytesPerRow => PaddedBytesPerRow(CPUBytesPerRow);
    int CPUBufferByteSize => Height * CPUBytesPerRow;
    int GPUBufferByteSize => Height * GPUBytesPerRow;
    private GPUBuffer GPUBuffer { get; }
    private IMemoryOwner<byte> BufferCPUMemoryOwner { get; }

    public async ValueTask<ReadOnlyMemory<byte>> ReadResultAsync(CancellationToken cancellation)
    {
        using var queue = Device.GetQueue();
        using var encoder = Device.CreateCommandEncoder(new());
        encoder.CopyTextureToBuffer(new GPUImageCopyTexture
        {
            Texture = Texture
        }, new GPUImageCopyBuffer
        {
            Buffer = GPUBuffer,
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
        using var cb = encoder.Finish(new());
        queue.Submit([cb]);

        using var _ = await GPUBuffer.MapAsync(GPUMapMode.Read, 0, GPUBufferByteSize, cancellation).ConfigureAwait(false);

        var gpuData = GPUBuffer.GetConstMappedRange(0, GPUBufferByteSize);
        for (var i = 0; i < Height; i++)
        {
            var offsetGPU = i * GPUBytesPerRow;
            var offsetCPU = i * CPUBytesPerRow;
            var size = CPUBytesPerRow;
            var cpuSpan = BufferCPUMemoryOwner.Memory.Span[offsetCPU..(offsetCPU + size)];
            var gpuSpan = gpuData[offsetGPU..(offsetGPU + size)];
            gpuSpan.CopyTo(cpuSpan);
        }

        return Memory;
    }

    public void Dispose()
    {
        BufferCPUMemoryOwner.Dispose();
        GPUBuffer.Dispose();
        Texture.Dispose();
    }
}

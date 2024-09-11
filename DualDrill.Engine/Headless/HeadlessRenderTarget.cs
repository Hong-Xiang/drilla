using DualDrill.Graphics;
using System.Buffers;

namespace DualDrill.Engine.Headless;

public sealed class HeadlessRenderTarget : IDisposable
{
    public HeadlessRenderTarget(
        IGPUDevice device,
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
                Width = (uint)Width,
                Height = (uint)Height,
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
    private IGPUDevice Device { get; }
    public int SlotIndex { get; }
    public int Width { get; }
    public int Height { get; }
    public GPUTextureFormat Format { get; }
    public IGPUTexture Texture { get; }
    public ReadOnlyMemory<byte> Memory => BufferCPUMemoryOwner.Memory[..CPUBufferByteSize];

    static int PaddedBytesPerRow(int byteSize) => byteSize + 255 & ~255;
    int PixelByteSize { get; } = 4;
    int CPUBytesPerRow => Width * PixelByteSize;
    int GPUBytesPerRow => PaddedBytesPerRow(CPUBytesPerRow);
    int CPUBufferByteSize => Height * CPUBytesPerRow;
    int GPUBufferByteSize => Height * GPUBytesPerRow;
    private IGPUBuffer GPUBuffer { get; }
    private IMemoryOwner<byte> BufferCPUMemoryOwner { get; }

    public async ValueTask<ReadOnlyMemory<byte>> ReadResultAsync(CancellationToken cancellation)
    {
        var queue = Device.Queue;
        using var encoder = Device.CreateCommandEncoder(new());
        encoder.CopyTextureToBuffer(new GPUImageCopyTexture
        {
            Texture = Texture
        }, new GPUImageCopyBuffer
        {
            Buffer = GPUBuffer,
            Layout = new GPUImageDataLayout
            {
                BytesPerRow = (uint)GPUBytesPerRow,
                Offset = 0,
                RowsPerImage = (uint)Height
            }
        }, new GPUExtent3D
        {
            Width = (uint)Width,
            Height = (uint)Height,
            DepthOrArrayLayers = 1
        });
        using var cb = encoder.Finish(new());
        queue.Submit([cb]);

        await GPUBuffer.MapAsync(GPUMapMode.Read, 0, (ulong)GPUBufferByteSize, cancellation);
        try
        {
            ReadOnlySpan<byte> gpuData = GPUBuffer.GetMappedRange(0, (ulong)GPUBufferByteSize);
            for (var i = 0; i < Height; i++)
            {
                var offsetGPU = i * GPUBytesPerRow;
                var offsetCPU = i * CPUBytesPerRow;
                var size = CPUBytesPerRow;
                var cpuSpan = BufferCPUMemoryOwner.Memory.Span[offsetCPU..(offsetCPU + size)];
                var gpuSpan = gpuData[offsetGPU..(offsetGPU + size)];
                gpuSpan.CopyTo(cpuSpan);
            }
        }
        finally
        {
            GPUBuffer.Unmap();
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

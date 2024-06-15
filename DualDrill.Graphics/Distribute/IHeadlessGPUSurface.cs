using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Buffers;
using System.Threading.Channels;

namespace DualDrill.Graphics.Distribute;

public interface IHeadlessGPUSurface
{
    public GPUTexture? TryGetCurrentTexture();
    public ValueTask<GPUTexture> GetCurrentTextureAsync(CancellationToken cancellation);
    public ValueTask Present(CancellationToken cancellation);
}

public sealed class HeadlessRenderTarget(
    GPUDevice Device,
    int Width,
    int Height,
    GPUTextureFormat Format)
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
        await queue.WaitSubmittedWorkDoneAsync(cancellation).ConfigureAwait(false);
        var bufferSizeGPU = (int)PaddedBufferSize(Width, Height);
        using var _ = await _bufferGPU.MapAsync(GPUMapMode.Read, 0, bufferSizeGPU, cancellation).ConfigureAwait(false);
        void ReadBytes()
        {
            var gpuData = _bufferGPU.GetConstMappedRange(0, bufferSizeGPU);
            gpuData.CopyTo(_bufferCPU.Memory.Span);
        }
        ReadBytes();
        return _bufferCPU.Memory;
    }
}

public readonly record struct RemoteSwapchainState(
    int SlotIndex,
    int Width,
    int Height
)
{
    public int BufferSize => 4 * Width * Height;
}

public readonly record struct ImagePush(
    int FrameIndex,
    int SlotIndex,
    int Width,
    int Height,
    GPUTextureFormat Format,
    ReadOnlyMemory<byte> Data)
{
}

public sealed class RemoteSwapchain(GPUDevice Device, int SlotCount) : IHeadlessGPUSurface
{
    sealed record class CacheResource(
        GPUTexture Texture,
        GPUBuffer Buffer,
        RemoteSwapchainState RemoteState
    )
    {
    }


    Channel<CacheResource> CurrentResourceChannel = Channel.CreateBounded<CacheResource>(SlotCount);
    Channel<CacheResource> PresentResourceChannel = Channel.CreateBounded<CacheResource>(SlotCount);
    Channel<ImagePush> ImagePushChannel = Channel.CreateBounded<ImagePush>(SlotCount);

    private readonly CacheResource?[] ResourceCache = new CacheResource?[SlotCount];

    public async Task AcceptNextImageSlot(ChannelReader<RemoteSwapchainState> states, CancellationToken cancellation)
    {
        await foreach (var state in states.ReadAllAsync(cancellation)
                                          .WithCancellation(cancellation)
                                          .ConfigureAwait(false))
        {
            var resource = ResourceCache[state.SlotIndex];
            if (resource is not null && resource.RemoteState != state)
            {
                resource.Texture.Dispose();
                resource.Buffer.Dispose();
                ResourceCache[resource.RemoteState.SlotIndex] = null;
                resource = null;
            }
            if (resource is null)
            {
                var texture = Device.CreateTexture(new GPUTextureDescriptor
                {
                    Usage = GPUTextureUsage.RenderAttachment | GPUTextureUsage.CopySrc,
                    MipLevelCount = 1,
                    SampleCount = 1,
                    Dimension = GPUTextureDimension.Dimension2D,
                    Size = new()
                    {
                        Width = state.Width,
                        Height = state.Height,
                        DepthOrArrayLayers = 1
                    },
                    Format = GPUTextureFormat.BGRA8UnormSrgb,
                });
                var buffer = Device.CreateBuffer(new GPUBufferDescriptor
                {
                    Usage = GPUBufferUsage.MapRead | GPUBufferUsage.CopyDst,
                    Size = (ulong)state.BufferSize
                });
                resource = new CacheResource(
                    texture,
                    buffer,
                    state
                );
            }
            await CurrentResourceChannel.Writer.WriteAsync(resource, cancellation).ConfigureAwait(false);
        }
    }

    public GPUTexture? TryGetCurrentTexture()
    {
        if (CurrentResourceChannel.Reader.TryRead(out var result))
        {
            return result.Texture;
        }
        else
        {
            return null;
        }
    }

    public async ValueTask<GPUTexture> GetCurrentTextureAsync(CancellationToken cancellation)
    {
        var result = await CurrentResourceChannel.Reader.ReadAsync(cancellation).ConfigureAwait(false);
        return result.Texture;
    }

    public async ValueTask Present(CancellationToken cancellation)
    {
        var reader = PresentResourceChannel.Reader;
        while (reader.TryRead(out var cache))
        {
            using var queue = Device.GetQueue();
            var state = cache.RemoteState;
            using var e = Device.CreateCommandEncoder(new());
            e.CopyTextureToBuffer(new GPUImageCopyTexture
            {
                Texture = cache.Texture
            }, new GPUImageCopyBuffer
            {
                Buffer = cache.Buffer,
                Layout = new GPUTextureDataLayout
                {
                    BytesPerRow = 4 * state.Width,
                    Offset = 0,
                    RowsPerImage = state.Height
                }
            }, new GPUExtent3D
            {
                Width = state.Width,
                Height = state.Height,
                DepthOrArrayLayers = 1
            });

            using var cb = e.Finish(new());
            queue.Submit([cb]);
            await queue.WaitSubmittedWorkDoneAsync(cancellation).ConfigureAwait(false);
            using var _ = await cache.Buffer.MapAsync(GPUMapMode.Read, 0, state.BufferSize, cancellation).ConfigureAwait(false);
            Image<Bgra32> ReadImage()
            {
                var byteData = cache.Buffer.GetConstMappedRange(0, state.BufferSize);
                return Image.LoadPixelData<Bgra32>(byteData, state.Width, state.Height);
            }
            var imagePush = new ImagePush
            {
                Image = (byte[])[],
                SlotIndex = state.SlotIndex
            };
            await ImagePushChannel.Writer.WriteAsync(imagePush, cancellation).ConfigureAwait(false);
        }
    }
}

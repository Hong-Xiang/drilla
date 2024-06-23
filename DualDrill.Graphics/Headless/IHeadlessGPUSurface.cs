using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Threading.Channels;

namespace DualDrill.Graphics.Headless;

public interface IHeadlessGPUSurface
{
    public GPUTexture? TryGetCurrentTexture();
    public ValueTask<GPUTexture> GetCurrentTextureAsync(CancellationToken cancellation);
    public ValueTask Present(CancellationToken cancellation);
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
    ReadOnlyMemory<byte> Data)
{
}

public sealed class HeadlessSurfaceLegacy(GPUDevice Device, int Width, int Height, int SlotCount) : IHeadlessGPUSurface
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

    private readonly HeadlessRenderTarget?[] RenderTargets = new HeadlessRenderTarget?[SlotCount];


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
            throw new NotImplementedException();
        }
    }
}

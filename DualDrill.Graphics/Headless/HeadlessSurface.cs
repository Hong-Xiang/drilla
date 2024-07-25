using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace DualDrill.Graphics.Headless;

public sealed class HeadlessSurface : IGPUSurface
{
    public sealed class Option
    {
        public int Width { get; set; } = 1472;
        public int Height { get; set; } = 936 * 2;
        //public int Width { get; set; } = 800;
        //public int Height { get; set; } = 600;
        public int SlotCount { get; set; } = 3;
        public GPUTextureFormat Format { get; set; } = GPUTextureFormat.BGRA8UnormSrgb;
    }
    private readonly Channel<HeadlessRenderTarget> RenderTargetChannel;
    private readonly Channel<(HeadlessRenderTarget, ReadOnlyMemory<byte>)> PresentedTargetChannel;
    GPUDevice Device;
    public readonly int Width;
    public readonly int Height;
    HeadlessRenderTarget? CurrentTarget = null;
    public HeadlessSurface(GPUDevice device, Option option)
    {
        Device = device;
        Width = option.Width;
        Height = option.Height;
        RenderTargetChannel = Channel.CreateBounded<HeadlessRenderTarget>(option.SlotCount);
        PresentedTargetChannel = Channel.CreateBounded<(HeadlessRenderTarget, ReadOnlyMemory<byte>)>(option.SlotCount);
        for (var i = 0; i < option.SlotCount; i++)
        {
            RenderTargetChannel.Writer.TryWrite(new HeadlessRenderTarget(Device, Width, Height, option.Format));
        }
    }
    public HeadlessRenderTarget? TryAcquireImage(int frameIndex)
    {
        if (RenderTargetChannel.Reader.TryRead(out var target))
        {
            CurrentTarget = target;
        }
        else
        {
            CurrentTarget = null;
        }
        return CurrentTarget;
    }

    /// <summary>
    /// Present of headless surface is simply copy GPU texture data into CPU buffer
    /// </summary>
    /// <returns></returns>
    public async ValueTask PresentAsync(CancellationToken cancellation)
    {
        var target = CurrentTarget;
        CurrentTarget = null;
        if (target is null)
        {
            return;
        }
        var data = await target.ReadResultAsync(cancellation).ConfigureAwait(false);
        await PresentedTargetChannel.Writer.WriteAsync((target, data), cancellation).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<ReadOnlyMemory<byte>> GetAllPresentedDataAsync([EnumeratorCancellation] CancellationToken cancellation)
    {
        await foreach (var presented in PresentedTargetChannel.Reader.ReadAllAsync(cancellation).ConfigureAwait(false))
        {
            var (renderTarget, resultMemory) = presented;
            yield return resultMemory;
            RenderTargetChannel.Writer.TryWrite(renderTarget);
        }
    }


    public GPUTexture? GetCurrentTexture()
    {
        return CurrentTarget?.Texture;
    }

    public void Configure(GPUSurfaceConfiguration configuration)
    {
        if (configuration.Width != Width || configuration.Height != Height)
        {
            throw new NotImplementedException($"HeadlessSurface does not support change surface size, current {Width}x{Height}, configured {configuration.Width}x{configuration.Height}");
        }
        Device = configuration.Device;
    }

    public void Unconfigure()
    {
    }
}

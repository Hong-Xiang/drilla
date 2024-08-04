using DualDrill.Graphics;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using WebSocketSharp;

namespace DualDrill.Engine.Headless;

public sealed class HeadlessSurface : IGPUSurface
{
    public sealed class Option
    {
        //public int Width { get; set; } = 1472;
        //public int Height { get; set; } = 936 * 2;
        public int Width { get; set; } = 640;
        public int Height { get; set; } = 480;
        public int SlotCount { get; set; } = 3;
        public GPUTextureFormat Format { get; set; } = GPUTextureFormat.BGRA8UnormSrgb;
    }
    public event EventHandler<HeadlessSurfaceFrame>? OnFrame;

    private readonly Channel<HeadlessRenderTarget> RenderTargetChannel;
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
        for (var i = 0; i < option.SlotCount; i++)
        {
            RenderTargetChannel.Writer.TryWrite(new HeadlessRenderTarget(Device, Width, Height, option.Format));
        }
    }

    public HeadlessRenderTarget? TryAcquireImage()
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

    public void Present()
    {
        var target = CurrentTarget;
        CurrentTarget = null;
        if (target is null)
        {
            return;
        }

        Task.Run(async () =>
        {
            var data = await target.ReadResultAsync(default).ConfigureAwait(false);
            var frame = new HeadlessSurfaceFrame(
                new GPUExtent3D
                {
                    Width = Width,
                    Height = Height,
                    DepthOrArrayLayers = 1
                },
                GPUTextureFormat.BGRA8Unorm,
                data
            );
            OnFrame?.Invoke(this, frame);
            await RenderTargetChannel.Writer.WriteAsync(target);
        });
    }
}
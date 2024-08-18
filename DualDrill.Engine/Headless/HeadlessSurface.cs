using DualDrill.Graphics;
using MessagePipe;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace DualDrill.Engine.Headless;


public sealed class HeadlessSurface : IGPUSurface
{
    public Guid Id { get; } = Guid.NewGuid();

    public sealed record class EntityDescription(Guid Id, Option Option)
    {
    }

    public sealed class Option
    {
        //public int Width { get; set; } = 1472;
        //public int Height { get; set; } = 936 * 2;
        //public int Width { get; set; } = 640;
        //public int Height { get; set; } = 480;
        public int Width { get; set; } = 800;
        public int Height { get; set; } = 600;

        public int SlotCount { get; set; } = 3;
        public GPUTextureFormat Format { get; set; } = GPUTextureFormat.BGRA8UnormSrgb;
    }

    public EntityDescription Entity => new(Id, new Option()
    {
        Width = Width,
        Height = Height,
        SlotCount = SlotCount,
        Format = Format
    });


    private readonly Channel<HeadlessRenderTarget> RenderTargetChannel;
    GPUDevice Device;
    public readonly int Width;
    public readonly int Height;
    public readonly GPUTextureFormat Format;
    public readonly int SlotCount;
    HeadlessRenderTarget? CurrentTarget = null;
    public IAsyncSubscriber<HeadlessSurfaceFrame> OnFrame { get; }
    private IAsyncPublisher<HeadlessSurfaceFrame> EmitOnFrame { get; }
    public HeadlessSurface(GPUDevice device, IOptions<Option> options, EventFactory eventFactory)
    {
        var option = options.Value;
        Device = device;
        Width = option.Width;
        Height = option.Height;
        Format = option.Format;
        SlotCount = option.SlotCount;
        RenderTargetChannel = Channel.CreateBounded<HeadlessRenderTarget>(option.SlotCount);
        for (var i = 0; i < option.SlotCount; i++)
        {
            RenderTargetChannel.Writer.TryWrite(new HeadlessRenderTarget(Device, Width, Height, option.Format, i));
        }
        (EmitOnFrame, OnFrame) = eventFactory.CreateAsyncEvent<HeadlessSurfaceFrame>();
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
                Format,
                data,
                target.SlotIndex
            );
            await EmitOnFrame.PublishAsync(frame);
            await RenderTargetChannel.Writer.WriteAsync(target);
        });
    }
}
using DualDrill.Graphics.Distribute;
using System.Buffers;
using System.Threading.Channels;

namespace DualDrill.Server;

public sealed class HeadlessRenderTargetPool
{
    public HeadlessRenderTargetPool(WGPUProviderService graphicsProvider, WGPUHeadlessService renderService)
    {
        GraphicsProvider = graphicsProvider;
        RenderService = renderService;
        Buffers = [];
        for (var i = 0; i < 32; i++)
        {
            Buffers.Add(new byte[renderService.Width * renderService.Height * 4]);
        }
    }


    Channel<HeadlessRenderTarget> PoolChannel = Channel.CreateUnbounded<HeadlessRenderTarget>();
    int CurrentBufferIndex = 0;
    List<byte[]> Buffers;

    public WGPUProviderService GraphicsProvider { get; }
    public WGPUHeadlessService RenderService { get; }

    public HeadlessRenderTarget Rent()
    {
        if (PoolChannel.Reader.TryRead(out var target))
        {
            return target;
        }
        Console.WriteLine($"Render Target With {RenderService.Width} Height {RenderService.Height}");
        return new HeadlessRenderTarget(GraphicsProvider.Device, RenderService.Width, RenderService.Height, RenderService.TextureFormat);
    }

    public byte[] RentResultBuffer()
    {
        lock (Buffers)
        {
            CurrentBufferIndex = (CurrentBufferIndex + 1) % Buffers.Count;
            return Buffers[CurrentBufferIndex];
        }

    }

    public void ReturnResultBuffer(byte[] data)
    {
    }

    public void Return(HeadlessRenderTarget renderTarget)
    {
        PoolChannel.Writer.TryWrite(renderTarget);
    }
}

﻿using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace DualDrill.Graphics.Headless;

enum RenderTargetState
{
    UndefinedData,
    RenderFinished,
    PresentFinished
}

sealed class RenderTargetWithState
{
    public required HeadlessRenderTarget RenderTarget { get; init; }
    public required RenderTargetState State { get; set; }
    public required int SlotIndex { get; init; }
}

public sealed class HeadlessSurface : IGPUSurface
{
    public sealed class Option
    {
        public int Width { get; set; } = 1472;
        public int Height { get; set; } = 936 * 2;
        public int SlotCount { get; set; } = 3;
        public GPUTextureFormat Format { get; set; } = GPUTextureFormat.BGRA8UnormSrgb;
    }

    GPUDevice Device;
    Channel<HeadlessRenderTarget> RenderTargetChannel;
    Channel<(HeadlessRenderTarget, ReadOnlyMemory<byte>)> PresentedTargetChannel;
    readonly int Width;
    readonly int Height;
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

    /// <summary>
    /// Copy Texture data into CPU buffer
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

    public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadAllPresentedDataAsync([EnumeratorCancellation] CancellationToken cancellation)
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

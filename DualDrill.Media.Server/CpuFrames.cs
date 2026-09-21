using Gst;
using Gst.App;

internal static class CpuFrames
{
    internal const int Width = 320;
    internal const int Height = 240;
    internal const int FramesPerSecond = VideoSettings.FramesPerSecond;
    internal const int BytesPerPixel = VideoSettings.BytesPerPixel;
    internal const int FrameBytes = Width * Height * BytesPerPixel;
    internal const ulong FrameDurationNanoseconds = ClockTime.NanosecondsPerSecond / FramesPerSecond;

    internal static void Fill(Span<byte> destination, ulong frameNumber)
    {
        Validate(destination);

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                Span<byte> pixel = destination.Slice((y * Width + x) * BytesPerPixel, BytesPerPixel);
                (pixel[0], pixel[1], pixel[2], pixel[3]) = RegionColor(x, y);
            }
        }

        int markerX = (int)(frameNumber * 5 % (Width - 24));
        int markerY = (int)(frameNumber * 3 % (Height - 24));

        for (int y = markerY; y < markerY + 24; y++)
        {
            for (int x = markerX; x < markerX + 24; x++)
            {
                Span<byte> pixel = destination.Slice((y * Width + x) * BytesPerPixel, BytesPerPixel);
                (pixel[0], pixel[1], pixel[2], pixel[3]) = (255, 255, 255, 255);
            }
        }
    }

    internal static void Validate(ReadOnlySpan<byte> pixels)
    {
        if (pixels.Length != FrameBytes)
        {
            throw new ArgumentException(
                $"Expected exactly {FrameBytes} BGRA bytes for {Width}x{Height}, received {pixels.Length}.",
                nameof(pixels));
        }
    }

    internal static int RunSelfTest()
    {
        byte[] first = new byte[FrameBytes];
        byte[] second = new byte[FrameBytes];
        Fill(first, 0);
        Fill(second, 1);

        if (Pixel(first, 200, 30) != (0, 255, 0, 255) ||
            Pixel(first, 30, 200) != (255, 0, 0, 255) ||
            Pixel(first, 200, 200) != (0, 255, 255, 255) ||
            first.AsSpan().SequenceEqual(second))
        {
            Console.Error.WriteLine("CPU frame self-test failed.");
            return 1;
        }

        try
        {
            Validate(first.AsSpan(1));
            Console.Error.WriteLine("CPU frame shape self-test failed.");
            return 1;
        }
        catch (ArgumentException)
        {
            Console.WriteLine("CPU frame generator and exact-shape validation passed.");
            return 0;
        }
    }

    private static (byte B, byte G, byte R, byte A) RegionColor(int x, int y) =>
        (x < Width / 2, y < Height / 2) switch
        {
            (true, true) => (0, 0, 255, 255),
            (false, true) => (0, 255, 0, 255),
            (true, false) => (255, 0, 0, 255),
            (false, false) => (0, 255, 255, 255),
        };

    private static (byte B, byte G, byte R, byte A) Pixel(byte[] pixels, int x, int y)
    {
        int offset = (y * Width + x) * BytesPerPixel;
        return (pixels[offset], pixels[offset + 1], pixels[offset + 2], pixels[offset + 3]);
    }
}

internal sealed class CpuBgraInput
{
    private readonly AppSrc source;
    private readonly VideoSettings video;

    internal CpuBgraInput(AppSrc source, VideoSettings video)
    {
        this.source = source;
        this.video = video;
        using Caps caps = Caps.FromString(
            $"video/x-raw,format=BGRA,width={video.Width},height={video.Height}," +
            $"framerate={VideoSettings.FramesPerSecond}/1")
            ?? throw new InvalidOperationException("Could not parse the raw BGRA caps.");
        source.SetCaps(caps);
        source.SetLive(true);
        source.Format = Format.Time;
        source.SetProperty("do-timestamp", true);
        source.MinLatency = 0;
        source.Block = false;
        source.EmitSignals = true;
        source.MaxBuffers = 2;
        source.LeakyType = AppLeakyType.Downstream;
    }

    internal FlowReturn Push(ReadOnlySpan<byte> pixels)
    {
        video.ValidateFrame(pixels);

        using Gst.Buffer buffer = Gst.Buffer.NewAllocate(null, checked((nuint)video.FrameBytes), null)
            ?? throw new InvalidOperationException("GStreamer could not allocate a CPU frame buffer.");

        buffer.SetDuration(ClockTime.FromNanoseconds(video.FrameDurationNanoseconds));

        using (Gst.Buffer.MapScope map = buffer.Map(MapFlags.Write))
        {
            pixels.CopyTo(map.Span);
        }

        return source.PushBuffer(buffer);
    }
}

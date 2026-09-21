using System.Globalization;

internal sealed record VideoSettings
{
    internal const int BytesPerPixel = 4;
    internal const int FramesPerSecond = 30;

    internal static VideoSettings Default { get; } = Create(320, 240, null);

    internal int Width { get; }
    internal int Height { get; }
    internal int? TargetBitrate { get; }
    internal int FrameBytes { get; }
    internal ulong FrameDurationNanoseconds { get; } = 1_000_000_000 / FramesPerSecond;

    private VideoSettings(int width, int height, int? targetBitrate, int frameBytes)
    {
        Width = width;
        Height = height;
        TargetBitrate = targetBitrate;
        FrameBytes = frameBytes;
    }

    internal static VideoSettings Load(IConfiguration configuration) =>
        Create(
            ParsePositiveInt(configuration, "Video:Width") ?? Default.Width,
            ParsePositiveInt(configuration, "Video:Height") ?? Default.Height,
            ParsePositiveInt(configuration, "Video:Bitrate"));

    internal void ValidateFrame(ReadOnlySpan<byte> pixels)
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
        var configured = new ConfigurationManager
        {
            ["Video:Width"] = "1920",
            ["Video:Height"] = "1080",
            ["Video:Bitrate"] = "8000000",
        };
        VideoSettings video = Load(configured);
        if (video != Create(1920, 1080, 8_000_000) ||
            Load(new ConfigurationManager()) != Default)
        {
            throw new InvalidOperationException("Video settings did not preserve defaults or parse the 1080p trial.");
        }

        AssertRejected("Video:Width", "1919");
        AssertRejected("Video:Height", "0");
        AssertRejected("Video:Bitrate", "0");
        AssertRejected("Video:Bitrate", "not-a-number");
        AssertRejected("Video:Width", "2147483646");
        AssertRejected("Video:Height", "2000000");
        Console.WriteLine("Video settings defaults, parsing, geometry, bitrate, and overflow validation passed.");
        return 0;
    }

    private static VideoSettings Create(int width, int height, int? targetBitrate)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width), $"Video dimensions must be positive; received {width}x{height}.");
        }
        if ((width & 1) != 0 || (height & 1) != 0)
        {
            throw new ArgumentException(
                $"VP8 video dimensions must be even; received {width}x{height}.");
        }
        if (targetBitrate is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetBitrate), $"Video bitrate must be positive; received {targetBitrate}.");
        }

        try
        {
            int rowBytes = checked(width * BytesPerPixel);
            int frameBytes = checked(rowBytes * height);
            _ = checked(((ulong)rowBytes + 255) * (ulong)height);
            return new(width, height, targetBitrate, frameBytes);
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width), $"Video dimensions {width}x{height} exceed supported buffer sizes.");
        }
    }

    private static int? ParsePositiveInt(IConfiguration configuration, string key)
    {
        string? text = configuration[key];
        if (text is null)
        {
            return null;
        }
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value <= 0)
        {
            throw new ArgumentException(
                $"Configuration \"{key}\" must be a positive integer; received \"{text}\".");
        }
        return value;
    }

    private static void AssertRejected(string key, string value)
    {
        var configuration = new ConfigurationManager { [key] = value };
        try
        {
            _ = Load(configuration);
        }
        catch (ArgumentException)
        {
            return;
        }
        throw new InvalidOperationException($"Invalid setting {key}={value} was accepted.");
    }
}

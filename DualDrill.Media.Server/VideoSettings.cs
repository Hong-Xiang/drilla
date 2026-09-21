using System.Globalization;

internal sealed record VideoSettings
{
    internal const int BytesPerPixel = 4;
    internal const int DefaultFramesPerSecond = 30;
    internal const int MinimumFramesPerSecond = 1;
    internal const int MaximumFramesPerSecond = 120;

    internal static VideoSettings Default { get; } = Create(320, 240, DefaultFramesPerSecond, null);

    internal int Width { get; }
    internal int Height { get; }
    internal int FramesPerSecond { get; }
    internal int? TargetBitrate { get; }
    internal int FrameBytes { get; }
    internal ulong FrameDurationNanoseconds { get; }
    internal TimeSpan FrameInterval { get; }

    private VideoSettings(
        int width,
        int height,
        int framesPerSecond,
        int? targetBitrate,
        int frameBytes,
        ulong frameDurationNanoseconds,
        TimeSpan frameInterval)
    {
        Width = width;
        Height = height;
        FramesPerSecond = framesPerSecond;
        TargetBitrate = targetBitrate;
        FrameBytes = frameBytes;
        FrameDurationNanoseconds = frameDurationNanoseconds;
        FrameInterval = frameInterval;
    }

    internal static VideoSettings Load(IConfiguration configuration) =>
        Create(
            ParsePositiveInt(configuration, "Video:Width") ?? Default.Width,
            ParsePositiveInt(configuration, "Video:Height") ?? Default.Height,
            ParseFramesPerSecond(configuration) ?? Default.FramesPerSecond,
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
            ["Video:FramesPerSecond"] = "60",
            ["Video:Bitrate"] = "8000000",
        };
        VideoSettings video = Load(configured);
        VideoSettings minimumRate = Load(
            new ConfigurationManager { ["Video:FramesPerSecond"] = "1" });
        VideoSettings maximumRate = Load(
            new ConfigurationManager { ["Video:FramesPerSecond"] = "120" });
        if (video != Create(1920, 1080, 60, 8_000_000) ||
            Load(new ConfigurationManager()) != Default)
        {
            throw new InvalidOperationException(
                "Video settings did not preserve the 30 fps default or parse the 60 fps trial.");
        }
        if (minimumRate.FrameDurationNanoseconds != 1_000_000_000 ||
            minimumRate.FrameInterval != TimeSpan.FromSeconds(1) ||
            maximumRate.FrameDurationNanoseconds != 8_333_333 ||
            maximumRate.FrameInterval != TimeSpan.FromTicks(83_333) ||
            maximumRate.FrameDurationNanoseconds == 0 ||
            maximumRate.FrameInterval == TimeSpan.Zero)
        {
            throw new InvalidOperationException("Video frame-duration bounds were not preserved.");
        }

        AssertRejected("Video:Width", "1919");
        AssertRejected("Video:Height", "0");
        AssertRejected("Video:Bitrate", "0");
        AssertRejected("Video:Bitrate", "not-a-number");
        AssertRejected("Video:FramesPerSecond", "");
        AssertRejected("Video:FramesPerSecond", "-1");
        AssertRejected("Video:FramesPerSecond", "0");
        AssertRejected("Video:FramesPerSecond", "1.5");
        AssertRejected("Video:FramesPerSecond", " 60");
        AssertRejected("Video:FramesPerSecond", "121");
        AssertRejected("Video:Width", "2147483646");
        AssertRejected("Video:Height", "2000000");
        Console.WriteLine(
            "Video settings defaults, frame rates, durations, geometry, bitrate, and overflow validation passed.");
        return 0;
    }

    private static VideoSettings Create(
        int width,
        int height,
        int framesPerSecond,
        int? targetBitrate)
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
        if (framesPerSecond is < MinimumFramesPerSecond or > MaximumFramesPerSecond)
        {
            throw new ArgumentOutOfRangeException(
                nameof(framesPerSecond),
                $"Video frame rate must be from {MinimumFramesPerSecond} through " +
                $"{MaximumFramesPerSecond} fps; received {framesPerSecond}.");
        }

        try
        {
            int rowBytes = checked(width * BytesPerPixel);
            int frameBytes = checked(rowBytes * height);
            _ = checked(((ulong)rowBytes + 255) * (ulong)height);
            ulong frameDurationNanoseconds =
                checked(1_000_000_000UL / (ulong)framesPerSecond);
            long frameIntervalTicks = checked(TimeSpan.TicksPerSecond / framesPerSecond);
            if (frameDurationNanoseconds == 0 || frameIntervalTicks == 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(framesPerSecond),
                    $"Video frame rate {framesPerSecond} produces a zero frame duration.");
            }
            return new(
                width,
                height,
                framesPerSecond,
                targetBitrate,
                frameBytes,
                frameDurationNanoseconds,
                TimeSpan.FromTicks(frameIntervalTicks));
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

    private static int? ParseFramesPerSecond(IConfiguration configuration)
    {
        const string Key = "Video:FramesPerSecond";
        string? text = configuration[Key];
        if (text is null)
        {
            return null;
        }
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) ||
            value is < MinimumFramesPerSecond or > MaximumFramesPerSecond)
        {
            throw new ArgumentException(
                $"Configuration \"{Key}\" must be an integer from {MinimumFramesPerSecond} through " +
                $"{MaximumFramesPerSecond}; received \"{text}\".");
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

using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace DualDrill.CLSL.NativeTest;

internal sealed record AntiAliasing
{
    public static readonly AntiAliasing One = new(1);
    public static readonly AntiAliasing Two = new(2);
    public static readonly AntiAliasing Three = new(3);

    private AntiAliasing(int value) => Value = value;

    public int Value { get; }
}

internal sealed record RaymarchProfile
{
    public RaymarchProfile(
        string name,
        int width,
        int height,
        float time,
        float mouseX,
        float mouseY,
        AntiAliasing antiAliasing)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(antiAliasing);

        Name = name;
        Width = width;
        Height = height;
        Time = time;
        MouseX = mouseX;
        MouseY = mouseY;
        AntiAliasing = antiAliasing;
    }

    public string Name { get; }
    public int Width { get; }
    public int Height { get; }
    public float Time { get; }
    public float MouseX { get; }
    public float MouseY { get; }
    public AntiAliasing AntiAliasing { get; }
}

internal sealed class RgbaImage
{
    public RgbaImage(int width, int height, byte[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(pixels);

        var expectedLength = checked((long)width * height * 4);
        if (pixels.LongLength != expectedLength)
        {
            throw new ArgumentException(
                $"RGBA8 image {width}x{height} requires {expectedLength} bytes; got {pixels.LongLength}.",
                nameof(pixels));
        }

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }
}

internal readonly record struct ImageThresholds(
    double MeanAbsoluteError,
    double RootMeanSquareError,
    int Percentile99,
    int Maximum)
{
    public static readonly ImageThresholds Raymarch = new(1.0, 4.0, 8, 64);
}

internal sealed record ImageDiffMetrics(
    double MeanAbsoluteError,
    double RootMeanSquareError,
    int Percentile99,
    int Maximum,
    long[] AbsoluteDeltaHistogram)
{
    public bool IsWithin(ImageThresholds limits) =>
        MeanAbsoluteError <= limits.MeanAbsoluteError
        && RootMeanSquareError <= limits.RootMeanSquareError
        && Percentile99 <= limits.Percentile99
        && Maximum <= limits.Maximum;

    public override string ToString() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"MAE={MeanAbsoluteError:F6}, RMSE={RootMeanSquareError:F6}, "
            + $"p99={Percentile99}, max={Maximum}, histogram={FormatHistogram()}");

    private string FormatHistogram() =>
        string.Join(
            ",",
            AbsoluteDeltaHistogram
                .Select((count, delta) => (count, delta))
                .Where(item => item.count != 0)
                .Select(item => $"{item.delta}:{item.count}"));
}

internal static class RgbaImageComparer
{
    public static ImageDiffMetrics Compare(RgbaImage reference, RgbaImage candidate)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(candidate);

        if (reference.Width != candidate.Width || reference.Height != candidate.Height)
        {
            throw new ArgumentException(
                $"Image dimensions differ: reference={reference.Width}x{reference.Height}, "
                + $"candidate={candidate.Width}x{candidate.Height}.",
                nameof(candidate));
        }

        if (reference.Pixels.Length != candidate.Pixels.Length)
        {
            throw new ArgumentException(
                $"Image byte lengths differ: reference={reference.Pixels.Length}, "
                + $"candidate={candidate.Pixels.Length}.",
                nameof(candidate));
        }

        var histogram = new long[256];
        long absoluteSum = 0;
        long squaredSum = 0;
        var maximum = 0;
        var channelCount = checked((long)reference.Width * reference.Height * 3);

        checked
        {
            for (var offset = 0; offset < reference.Pixels.Length; offset += 4)
            {
                if (reference.Pixels[offset + 3] != 255 || candidate.Pixels[offset + 3] != 255)
                {
                    throw new InvalidDataException(
                        $"Alpha must be exactly 255 at pixel {offset / 4}; "
                        + $"reference={reference.Pixels[offset + 3]}, "
                        + $"candidate={candidate.Pixels[offset + 3]}.");
                }

                for (var channel = 0; channel < 3; channel++)
                {
                    var delta = Math.Abs(
                        reference.Pixels[offset + channel] - candidate.Pixels[offset + channel]);
                    histogram[delta]++;
                    absoluteSum += delta;
                    squaredSum += (long)delta * delta;
                    maximum = Math.Max(maximum, delta);
                }
            }
        }

        var percentileTarget = (long)Math.Ceiling(channelCount * 0.99);
        long cumulative = 0;
        var percentile99 = 0;
        for (; percentile99 < histogram.Length; percentile99++)
        {
            cumulative += histogram[percentile99];
            if (cumulative >= percentileTarget)
                break;
        }

        return new(
            (double)absoluteSum / channelCount,
            Math.Sqrt((double)squaredSum / channelCount),
            percentile99,
            maximum,
            histogram);
    }
}

internal static class RaymarchReference
{
    public const string Commit = "5db58051fa019b591e4e13a53b1dceef8e37ea4f";
    public const string Sha256 = "59e27ceb51248f33c7228ca458f443c6fcd1fb16c09a5975ad277f1adfe8bdda";
    public const string FileName = "reference-Xds3zN.glslf";
    public const string AaMacro = "#define AA 1";

    public static string SourcePath =>
        Path.Combine(AppContext.BaseDirectory, "Reference", FileName);

    public static byte[] ReadPristineBytes() => File.ReadAllBytes(SourcePath);

    public static string ReadPristineSource() =>
        Encoding.UTF8.GetString(ReadPristineBytes());

    public static string ComputeSha256(byte[] source) =>
        Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant();

    public static int CountAaMacros(string source) =>
        source.Split(AaMacro, StringSplitOptions.None).Length - 1;

    public static string SourceFor(AntiAliasing antiAliasing)
    {
        ArgumentNullException.ThrowIfNull(antiAliasing);
        var source = ReadPristineSource();
        var macroCount = CountAaMacros(source);
        if (macroCount != 2)
            throw new InvalidDataException($"Expected exactly two '{AaMacro}' macros; found {macroCount}.");

        return antiAliasing == AntiAliasing.One
            ? source
            : source.Replace(
                AaMacro,
                $"#define AA {antiAliasing.Value}",
                StringComparison.Ordinal);
    }

    public static async Task<string> CompileFragmentAsync(
        RaymarchProfile profile,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var scratch = Path.Combine(
            AppContext.BaseDirectory,
            ".raymarch-reference",
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(scratch);

        try
        {
            var referencePath = Path.Combine(scratch, FileName);
            var wrapperPath = Path.Combine(scratch, "reference.frag");
            var outputPath = Path.Combine(scratch, "reference.wgsl");
            await File.WriteAllTextAsync(
                referencePath,
                SourceFor(profile.AntiAliasing),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellation);
            await File.WriteAllTextAsync(
                wrapperPath,
                BuildWrapper(profile),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellation);

            await RunSlangAsync(
                [
                    "-lang", "glsl",
                    "-target", "wgsl",
                    "-entry", "main",
                    "-stage", "fragment",
                    wrapperPath,
                    "-o", outputPath
                ],
                cancellation);
            return await File.ReadAllTextAsync(outputPath, cancellation);
        }
        finally
        {
            if (Directory.Exists(scratch))
                Directory.Delete(scratch, recursive: true);
        }
    }

    private static string BuildWrapper(RaymarchProfile profile) =>
        $$"""
        #version 450
        #define HW_PERFORMANCE 0
        const vec3 iResolution = vec3({{GlslFloat(profile.Width)}}, {{GlslFloat(profile.Height)}}, 1.0);
        const float iTime = {{GlslFloat(profile.Time)}};
        const vec4 iMouse = vec4({{GlslFloat(profile.MouseX)}}, {{GlslFloat(profile.MouseY)}}, 0.0, 0.0);
        const int iFrame = 0;
        layout(location = 0) out vec4 color;
        #include "{{FileName}}"
        void main()
        {
            vec4 fragmentColor;
            mainImage(fragmentColor, vec2(gl_FragCoord.x, iResolution.y - gl_FragCoord.y));
            color = fragmentColor;
        }
        """;

    private static string GlslFloat(float value)
    {
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        return text.Contains('.', StringComparison.Ordinal)
            || text.Contains('E', StringComparison.OrdinalIgnoreCase)
                ? text
                : $"{text}.0";
    }

    private static async Task RunSlangAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellation)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "slangc",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidOperationException("Failed to start slangc.");

        var stdout = process.StandardOutput.ReadToEndAsync(cancellation);
        var stderr = process.StandardError.ReadToEndAsync(cancellation);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(stdout, stderr);
            throw;
        }

        var standardOutput = await stdout;
        var standardError = await stderr;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"slangc GLSL-to-WGSL compilation failed with exit code {process.ExitCode}."
                + $"\nStandard output:\n{standardOutput}"
                + $"\nStandard error:\n{standardError}");
        }
    }
}

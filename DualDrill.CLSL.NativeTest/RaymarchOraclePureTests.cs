namespace DualDrill.CLSL.NativeTest;

public sealed class RaymarchOraclePureTests
{
    private static readonly RaymarchProfile DefaultProfile = new(
        "center-aa1",
        320,
        180,
        1.0f,
        160.0f,
        90.0f,
        AntiAliasing.One);

    [Fact]
    public void Pinned_reference_is_pristine_and_has_exactly_two_AA_macros()
    {
        var bytes = RaymarchReference.ReadPristineBytes();
        var source = RaymarchReference.ReadPristineSource();

        Assert.Equal(RaymarchReference.Sha256, RaymarchReference.ComputeSha256(bytes));
        Assert.Equal(2, RaymarchReference.CountAaMacros(source));
        Assert.Equal(bytes, System.Text.Encoding.UTF8.GetBytes(source));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Derived_AA_source_changes_only_the_two_expected_macros(int samples)
    {
        var antiAliasing = samples switch
        {
            1 => AntiAliasing.One,
            2 => AntiAliasing.Two,
            3 => AntiAliasing.Three,
            _ => throw new ArgumentOutOfRangeException(nameof(samples)),
        };
        var pristine = RaymarchReference.ReadPristineSource();
        var derived = RaymarchReference.SourceFor(antiAliasing);

        Assert.Equal(
            pristine.Replace(
                RaymarchReference.AaMacro,
                $"#define AA {samples}",
                StringComparison.Ordinal),
            derived);
        Assert.Equal(
            2,
            derived.Split($"#define AA {samples}", StringSplitOptions.None).Length - 1);
        Assert.Equal(RaymarchReference.Sha256, RaymarchReference.ComputeSha256(
            RaymarchReference.ReadPristineBytes()));
    }

    [Fact]
    public async Task Pinned_GLSL_reference_compiles_directly_to_WGSL()
    {
        foreach (var antiAliasing in new[]
        {
            AntiAliasing.One,
            AntiAliasing.Two,
            AntiAliasing.Three,
        })
        {
            var wgsl = await RaymarchReference.CompileFragmentAsync(
                new(
                    DefaultProfile.Name,
                    DefaultProfile.Width,
                    DefaultProfile.Height,
                    DefaultProfile.Time,
                    DefaultProfile.MouseX,
                    DefaultProfile.MouseY,
                    antiAliasing));

            Assert.False(string.IsNullOrWhiteSpace(wgsl));
            Assert.Contains("@fragment", wgsl);
            Assert.Contains("fn main", wgsl);
        }
    }

    [Fact]
    public void Identity_comparison_is_exactly_zero()
    {
        var image = SolidImage(2, 2, 25, 50, 75);

        var metrics = RgbaImageComparer.Compare(image, image);

        Assert.Equal(0.0, metrics.MeanAbsoluteError);
        Assert.Equal(0.0, metrics.RootMeanSquareError);
        Assert.Equal(0, metrics.Percentile99);
        Assert.Equal(0, metrics.Maximum);
        Assert.True(metrics.IsWithin(ImageThresholds.Raymarch));
        Assert.Equal(12, metrics.AbsoluteDeltaHistogram[0]);
    }

    [Fact]
    public void Comparator_reports_RGB_error_distribution()
    {
        var reference = new RgbaImage(1, 1, [0, 0, 0, 255]);
        var candidate = new RgbaImage(1, 1, [1, 2, 3, 255]);

        var metrics = RgbaImageComparer.Compare(reference, candidate);

        Assert.Equal(2.0, metrics.MeanAbsoluteError);
        Assert.Equal(Math.Sqrt(14.0 / 3.0), metrics.RootMeanSquareError);
        Assert.Equal(3, metrics.Percentile99);
        Assert.Equal(3, metrics.Maximum);
        Assert.Equal(1, metrics.AbsoluteDeltaHistogram[1]);
        Assert.Equal(1, metrics.AbsoluteDeltaHistogram[2]);
        Assert.Equal(1, metrics.AbsoluteDeltaHistogram[3]);
    }

    [Fact]
    public void X_flip_negative_control_fails_the_single_acceptance_predicate()
    {
        var pixels = new byte[]
        {
            0, 10, 20, 255,
            200, 210, 220, 255,
        };
        var reference = new RgbaImage(2, 1, pixels);
        var flipped = new RgbaImage(
            2,
            1,
            [.. pixels.AsSpan(4, 4), .. pixels.AsSpan(0, 4)]);

        var metrics = RgbaImageComparer.Compare(reference, flipped);

        Assert.False(metrics.IsWithin(ImageThresholds.Raymarch));
    }

    [Fact]
    public void Invalid_dimensions_lengths_and_alpha_fail_clearly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RgbaImage(0, 1, []));
        var lengthError = Assert.Throws<ArgumentException>(() => new RgbaImage(2, 2, new byte[15]));
        Assert.Contains("requires 16 bytes", lengthError.Message);

        var reference = SolidImage(1, 1, 0, 0, 0);
        var dimensionError = Assert.Throws<ArgumentException>(
            () => RgbaImageComparer.Compare(reference, SolidImage(2, 1, 0, 0, 0)));
        Assert.Contains("dimensions differ", dimensionError.Message);

        var invalidAlpha = new RgbaImage(1, 1, [0, 0, 0, 254]);
        var alphaError = Assert.Throws<InvalidDataException>(
            () => RgbaImageComparer.Compare(reference, invalidAlpha));
        Assert.Contains("Alpha must be exactly 255", alphaError.Message);
    }

    private static RgbaImage SolidImage(int width, int height, byte red, byte green, byte blue)
    {
        var pixels = new byte[checked(width * height * 4)];
        for (var offset = 0; offset < pixels.Length; offset += 4)
        {
            pixels[offset] = red;
            pixels[offset + 1] = green;
            pixels[offset + 2] = blue;
            pixels[offset + 3] = 255;
        }

        return new(width, height, pixels);
    }
}

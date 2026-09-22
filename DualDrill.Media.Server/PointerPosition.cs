internal sealed record PointerPosition
{
    internal static PointerPosition Center { get; } = Create(0.5, 0.5);

    internal float X { get; }
    internal float Y { get; }
    internal float ClipX => 2 * X - 1;
    internal float ClipY => 1 - 2 * Y;

    private PointerPosition(float x, float y)
    {
        X = x;
        Y = y;
    }

    internal static PointerPosition Create(double x, double y)
    {
        if (!double.IsFinite(x) || x is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(x), x, "Pointer x must be finite and normalized.");
        }
        if (!double.IsFinite(y) || y is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(y), y, "Pointer y must be finite and normalized.");
        }
        return new((float)x, (float)y);
    }

    internal static int RunSelfTest()
    {
        PointerPosition topLeft = Create(0, 0);
        PointerPosition bottomRight = Create(1, 1);
        if (Center != Create(0.5, 0.5) ||
            topLeft.ClipX != -1 || topLeft.ClipY != 1 ||
            bottomRight.ClipX != 1 || bottomRight.ClipY != -1)
        {
            throw new InvalidOperationException("Pointer normalization boundary laws failed.");
        }

        foreach ((double x, double y) in new[]
        {
            (double.NaN, 0.5),
            (double.PositiveInfinity, 0.5),
            (0.5, double.NegativeInfinity),
            (-double.Epsilon, 0.5),
            (0.5, Math.BitIncrement(1d)),
        })
        {
            try
            {
                _ = Create(x, y);
                throw new InvalidOperationException($"Invalid pointer position ({x}, {y}) was accepted.");
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }

        Console.WriteLine("Pointer normalization boundaries and finite range laws passed.");
        return 0;
    }
}

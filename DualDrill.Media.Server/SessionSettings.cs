using System.Globalization;

internal sealed record SessionSettings(int MaxConcurrent)
{
    internal const int DefaultMaxConcurrent = 4;

    internal static SessionSettings Load(IConfiguration configuration)
    {
        const string Key = "Sessions:MaxConcurrent";
        string? text = configuration[Key];
        if (text is null)
        {
            return new(DefaultMaxConcurrent);
        }
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) ||
            value <= 0)
        {
            throw new ArgumentException(
                $"Configuration \"{Key}\" must be a positive integer; received \"{text}\".");
        }
        return new(value);
    }

    internal static int RunSelfTest()
    {
        if (Load(new ConfigurationManager()).MaxConcurrent != DefaultMaxConcurrent ||
            Load(new ConfigurationManager { ["Sessions:MaxConcurrent"] = "2" }).MaxConcurrent != 2)
        {
            throw new InvalidOperationException("Media session capacity parsing failed.");
        }

        foreach (string value in new[] { "", "0", "-1", "1.5", " 4", "2147483648" })
        {
            try
            {
                _ = Load(new ConfigurationManager { ["Sessions:MaxConcurrent"] = value });
                throw new InvalidOperationException(
                    $"Invalid media session capacity \"{value}\" was accepted.");
            }
            catch (ArgumentException)
            {
            }
        }

        var gate = new SessionGate(2);
        SessionAdmission first = gate.TryAcquire()
            ?? throw new InvalidOperationException("The first media session was not admitted.");
        using SessionAdmission second = gate.TryAcquire()
            ?? throw new InvalidOperationException("The second media session was not admitted.");
        if (gate.TryAcquire() is not null)
        {
            throw new InvalidOperationException("The media session capacity was exceeded.");
        }
        first.Dispose();
        first.Dispose();
        using SessionAdmission replacement = gate.TryAcquire()
            ?? throw new InvalidOperationException("A released media session slot was not reusable.");

        AssertConcurrentCapacity();
        Console.WriteLine(
            "Media session default, strict configuration, capacity, concurrent admission, and release passed.");
        return 0;
    }

    private static void AssertConcurrentCapacity()
    {
        const int Capacity = 4;
        const int Attempts = 8;
        var gate = new SessionGate(Capacity);
        using var start = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var attempted = new CountdownEvent(Attempts);
        int admitted = 0;
        Task[] tasks = Enumerable.Range(0, Attempts)
            .Select(_ => Task.Factory.StartNew(
                () =>
                {
                    start.Wait();
                    using SessionAdmission? admission = gate.TryAcquire();
                    if (admission is not null)
                    {
                        Interlocked.Increment(ref admitted);
                    }
                    attempted.Signal();
                    if (admission is not null)
                    {
                        release.Wait();
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default))
            .ToArray();

        start.Set();
        if (!attempted.Wait(TimeSpan.FromSeconds(5)))
        {
            release.Set();
            Task.WaitAll(tasks);
            throw new TimeoutException("Concurrent media session admission did not complete.");
        }
        if (Volatile.Read(ref admitted) != Capacity)
        {
            release.Set();
            Task.WaitAll(tasks);
            throw new InvalidOperationException(
                $"Expected {Capacity} concurrent admissions, received {admitted}.");
        }

        release.Set();
        Task.WaitAll(tasks);
        SessionAdmission[] reused = Enumerable.Range(0, Capacity)
            .Select(_ => gate.TryAcquire()
                ?? throw new InvalidOperationException("A concurrently released slot was not reusable."))
            .ToArray();
        try
        {
            if (gate.TryAcquire() is not null)
            {
                throw new InvalidOperationException("Concurrent release overfilled the media session gate.");
            }
        }
        finally
        {
            foreach (SessionAdmission admission in reused)
            {
                admission.Dispose();
            }
        }
    }
}

internal sealed class SessionGate(int capacity)
{
    private readonly SemaphoreSlim slots = new(capacity, capacity);

    internal SessionAdmission? TryAcquire() =>
        slots.Wait(0) ? new SessionAdmission(this) : null;

    internal void Release() => slots.Release();
}

internal sealed class SessionAdmission(SessionGate gate) : IDisposable
{
    private SessionGate? owner = gate;

    public void Dispose() => Interlocked.Exchange(ref owner, null)?.Release();
}

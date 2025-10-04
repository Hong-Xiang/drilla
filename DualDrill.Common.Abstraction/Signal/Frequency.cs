namespace DualDrill.Common.Abstraction.Signal;

public interface IFrequency
{
    public static abstract int Frequency { get; }
}

public static class Frequency
{
    public static readonly int BaseTimestampFrequency = 7200000;
    public sealed class SystemTick : IFrequency
    {
        public static int Frequency => 10_000_000;
    }
    public sealed class VideoRTCTimestamp : IFrequency
    {
        public static int Frequency => 90000;
    }
    public sealed class RealtimeFrame : IFrequency
    {
        public static int Frequency => 60;
    }

    public static int TimeUnit<TFrequency>()
        where TFrequency : IFrequency => TFrequency.Frequency / BaseTimestampFrequency;
    public static int HalfTimeUnit<TFrequency>()
        where TFrequency : IFrequency => TimeUnit<TFrequency>() / 2;

    public static int ResampleCount<TSampler, TTarget>()
        where TSampler : IFrequency
        where TTarget : IFrequency
    {
        if (TSampler.Frequency % TTarget.Frequency == 0)
        {
            return TSampler.Frequency / TTarget.Frequency;
        }
        else
        {
            double sourcePeriod = (1.0) / TSampler.Frequency;
            double targetPeriod = (1.0) / TTarget.Frequency;
            return (int)Math.Floor(sourcePeriod / targetPeriod);
        }
    }
}



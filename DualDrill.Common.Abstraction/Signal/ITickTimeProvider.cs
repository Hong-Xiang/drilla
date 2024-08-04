namespace DualDrill.Common.Abstraction.Signal;

public interface ITickTimeProvider<TFrequency>
    where TFrequency : IFrequency
{
    public DateTimeOffset GetTime(Tick<TFrequency> tick);
    public Tick<TFrequency> GetTick(DateTimeOffset time);
}


public sealed class TickTimeProvider<TFrequency>(DateTimeOffset ZeroTime) : ITickTimeProvider<TFrequency>
    where TFrequency : IFrequency
{
    public DateTimeOffset GetTime(Tick<TFrequency> tick)
    {
        return ZeroTime + tick.ToFrequency<Frequency.SystemTick>().AsTimeSpan();
    }

    public Tick<TFrequency> GetTick(DateTimeOffset time)
    {
        return Tick.FromTimeSpan(time - ZeroTime).ToFrequency<TFrequency>();
    }
}

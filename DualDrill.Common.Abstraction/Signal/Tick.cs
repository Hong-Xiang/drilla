using System.Numerics;

namespace DualDrill.Common.Abstraction.Signal;

public readonly record struct Tick<T>(long Index)
    : IComparable<Tick<T>>,
      IComparisonOperators<Tick<T>, Tick<T>, bool>
    where T : IFrequency
{

    public static Tick<T> Zero => new(0);

    public int CompareTo(Tick<T> other)
    {
        return Index.CompareTo(other.Index);
    }

    public static bool operator <(Tick<T> left, Tick<T> right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(Tick<T> left, Tick<T> right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(Tick<T> left, Tick<T> right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(Tick<T> left, Tick<T> right)
    {
        return left.CompareTo(right) >= 0;
    }

    public Tick<TTargetFrequency> ToFrequency<TTargetFrequency>()
        where TTargetFrequency : IFrequency
    {
        return new Tick<TTargetFrequency>(Index * TTargetFrequency.Frequency / T.Frequency);
    }
}

public static class Tick
{

    public static TimeSpan AsTimeSpan(this Tick<Frequency.SystemTick> tick)
    {
        return new TimeSpan(tick.Index);
    }

    public static Tick<Frequency.SystemTick> FromTimeSpan(this TimeSpan timeSpan)
    {
        return new Tick<Frequency.SystemTick>(timeSpan.Ticks);
    }
}

sealed class OutdatedSignalException<TFrequency>(
    Tick<TFrequency> Minimum,
    Tick<TFrequency> Sampling
) : Exception($"Outdated signal, current {Minimum}, sampling {Sampling}")
    where TFrequency : IFrequency
{
}





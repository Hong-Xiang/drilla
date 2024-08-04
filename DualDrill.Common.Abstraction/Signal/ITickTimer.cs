namespace DualDrill.Common.Abstraction.Signal;

public interface ITickTimer<TFrequency>
    where TFrequency : IFrequency
{
    IDisposable Trigger<TSignal, T>(TSignal signal)
        where TSignal : ISignal<TFrequency, T>;
}

public interface ITickTimerProvider<TFrequency>
    where TFrequency : IFrequency
{
    Tick<TFrequency> DoTick();
    ITickTimer<TFrequency> Clock { get; }
}


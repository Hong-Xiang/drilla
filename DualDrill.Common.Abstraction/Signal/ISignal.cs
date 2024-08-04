namespace DualDrill.Common.Abstraction.Signal;

public interface ISignal<TFrequency, T>
    where TFrequency : IFrequency
    where T : allows ref struct

{
    T Sample(Tick<TFrequency> t);
}



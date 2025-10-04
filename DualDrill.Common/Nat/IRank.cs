namespace DualDrill.Common.Nat;

public interface IRank : INat
{
}

public interface IRank<TSelf> : IRank, INat<TSelf>
    where TSelf : IRank<TSelf>
{
}

public sealed partial class N2 : IRank<N2> { }
public sealed partial class N3 : IRank<N3> { }
public sealed partial class N4 : IRank<N4> { }

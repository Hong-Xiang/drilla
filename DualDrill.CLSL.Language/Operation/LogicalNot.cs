using DualDrill.Common;

namespace DualDrill.CLSL.Language.Operation;

public readonly record struct LogicalNot : ISingleton<LogicalNot>
{
    public static LogicalNot Instance => new();
}

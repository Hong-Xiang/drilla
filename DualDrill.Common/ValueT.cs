namespace DualDrill.Common;

/// <summary>
/// a type map from a singleton class type to logically singleton value type
/// </summary>
/// <typeparam name="TSingleton"></typeparam>
public readonly record struct ValueT<TSingleton>
    where TSingleton : class, DotNext.Patterns.ISingleton<TSingleton>
{
}

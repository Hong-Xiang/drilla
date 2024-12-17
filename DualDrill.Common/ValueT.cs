using DotNext.Patterns;

namespace DualDrill.Common;

/// <summary>
/// a type map from a singleton class type to logically singleton value type
/// </summary>
/// <typeparam name="TSingleton"></typeparam>
public struct ValueT<TSingleton>
    where TSingleton : class, ISingleton<TSingleton>
{
}

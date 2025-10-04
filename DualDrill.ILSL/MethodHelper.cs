using System.Reflection;

namespace DualDrill.CLSL;

public static class MethodHelper
{
    public static MethodBase GetMethod<T>(this Action<T> f) => f.Method;
    public static MethodBase GetMethod<TA, TB>(this Action<TA, TB> f) => f.Method;
    public static MethodBase GetMethod<TA, TB, TC>(this Action<TA, TB, TC> f) => f.Method;
    public static MethodBase GetMethod<TA, TB, TC, TD>(this Action<TA, TB, TC, TD> f) => f.Method;
    public static MethodBase GetMethod<TA, TB, TC, TD, TE>(this Action<TA, TB, TC, TD, TE> f) => f.Method;

    public static MethodBase GetMethod<T>(this Func<T> f) => f.Method;
    public static MethodBase GetMethod<TA, TB>(this Func<TA, TB> f) => f.Method;
    public static MethodBase GetMethod<TA, TB, TC>(this Func<TA, TB, TC> f) => f.Method;
    public static MethodBase GetMethod<TA, TB, TC, TD>(this Func<TA, TB, TC, TD> f) => f.Method;
    public static MethodBase GetMethod<TA, TB, TC, TD, TE>(this Func<TA, TB, TC, TD, TE> f) => f.Method;
}
﻿using System.Reflection;

namespace DualDrill.ILSL;

public static class MethodHelper
{
    public static MethodBase GetMethod<T>(this Action<T> f) => f.Method;
    public static MethodBase GetMethod<TA, TB>(this Action<TA, TB> f) => f.Method;
    public static MethodBase GetMethod<TA, TB, TC>(this Action<TA, TB, TC> f) => f.Method;
    public static MethodBase GetMethod<TA, TB, TC, TD>(this Action<TA, TB, TC, TD> f) => f.Method;

    public static MethodBase GetMethod<T>(this Func<T> f) => f.Method;
    public static MethodBase GetMethod<TA, TB>(this Func<TA, TB> f) => f.Method;
    public static MethodBase GetMethod<TA, TB, TC>(this Func<TA, TB, TC> f) => f.Method;
    public static MethodBase GetMethod<TA, TB, TC, TD>(this Func<TA, TB, TC, TD> f) => f.Method;
}

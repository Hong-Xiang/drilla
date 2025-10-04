using System.Reflection;

namespace DualDrill.CLSL.Frontend;

public sealed class ValidationException(string message, MethodBase method)
    : Exception(message + $" @ {method.Name}")
{
    public MethodBase Method { get; } = method;
}
using System.Reflection;

namespace DualDrill.CLSL.Frontend;

public sealed class ValidationException(string message, MethodBase method, Exception? innerException = null)
    : Exception(message + $" @ {method.Name}", innerException)
{
    public MethodBase Method { get; } = method;
}
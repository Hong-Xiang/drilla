using System.Collections.Immutable;
using System.Reflection;

namespace DualDrill.CLSL.Frontend.ReflectionShader;

public sealed record class ReflectionShaderSource(
    ImmutableHashSet<Type> ShaderFragments,
    ImmutableHashSet<MethodBase> TopLevelMethods
)
{
    public ReflectionShaderSource AddShader<T>()
        => this with { ShaderFragments = ShaderFragments.Add(typeof(T)) };

    public ReflectionShaderSource AddMethod(MethodBase method)
        => this with { TopLevelMethods = TopLevelMethods.Add(method) };
}

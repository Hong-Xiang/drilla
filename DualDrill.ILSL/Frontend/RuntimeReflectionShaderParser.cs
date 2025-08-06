using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Reflection;

namespace DualDrill.CLSL.Frontend;

public sealed class RuntimeReflectionShaderParser(RuntimeReflectionShaderParser.Option Option)
{
    public sealed record class Option { }

    public void AddShader<T>()
    {
        throw new NotImplementedException();
    }

    public void AddMethod(MethodBase method)
    {
        throw new NotImplementedException();
    }
}

public sealed record class ReflectionShaderSource(
    IReadOnlyList<Type> ShaderFragments,
    IReadOnlyList<MethodBase> TopLevelMethods,
    IReadOnlySet<Type> ReferencedTypes,
    IReadOnlySet<MethodBase> ReferencedMethods,
    IReadOnlySet<FieldInfo> ReferencedModuleField
)
{
}
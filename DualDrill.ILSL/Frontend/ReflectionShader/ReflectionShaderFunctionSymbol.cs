using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.ShaderAttribute;
using System.Collections.Immutable;
using System.Reflection;

namespace DualDrill.CLSL.Frontend.ReflectionShader;

public sealed class ReflectionShaderFunctionSymbol(RuntimeReflectionShaderParser Parser, MethodBase Method) : IFunctionSymbol
{
    MethodBase Method { get; } = Method;
    public string Name => Method.Name;
    public ImmutableArray<IShaderAttribute> Attributes => Method.GetCustomAttributes().Select(attr => new ReflectionShaderAttributeSymbol(attr)).ToImmutableArray();
    public IFunctionBodySymbol Body => throw new NotImplementedException();
    public ImmutableArray<IParameterSymbol> Parameters => Method.GetParameters().Select(param => new ReflectionShaderParameterSymbol(param)).ToImmutableArray();
    readonly Lazy<ITypeSymbol> _lazyReturnType = new(() =>
    {
        if (Method is MethodInfo methodInfo)
        {
            return Parser.ParseType(methodInfo.ReturnType);
        }
        if (Method is ConstructorInfo constructorInfo && constructorInfo.DeclaringType is not null)
        {
            return Parser.ParseType(constructorInfo.DeclaringType);
        }
        throw new NotImplementedException();
    });
    public ITypeSymbol ReturnType => _lazyReturnType.Value;
}

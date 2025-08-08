using DualDrill.CLSL.Language.Declaration;
using System.Reflection;

namespace DualDrill.CLSL.Frontend.ReflectionShader;

public sealed class RuntimeReflectionShaderParser
{
    Dictionary<MethodBase, IFunctionSymbol> Functions { get; } = [];
    Dictionary<Type, ITypeSymbol> Types { get; } = [];
    public RuntimeReflectionShaderParser(ParserOption option)
    {
        Option = option;
    }
    public ParserOption Option { get; }

    public sealed record class ParserOption { }

    public IModuleSymbol<CilFunctionBody> Parse(ReflectionShaderSource source)
    {
        throw new NotImplementedException();
    }

    internal IFunctionSymbol ParseFunction(MethodBase method)
    {
        if (Functions.TryGetValue(method, out var symbol))
        {
            return symbol;
        }
        throw new NotImplementedException();
    }

    internal ITypeSymbol ParseType(Type type)
    {
        if (Types.TryGetValue(type, out var symbol))
        {
            return symbol;
        }
        symbol = new ReflectionShaderTypeSymbol(this, type);
        Types[type] = symbol;
        return symbol;
    }
}

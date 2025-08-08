using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;
using System.Reflection;

namespace DualDrill.CLSL.Frontend.ReflectionShader;

public sealed class ReflectionShaderTypeSymbol : ISymbol
{
    public ReflectionShaderTypeSymbol(RuntimeReflectionShaderParser parser, Type type)
    {
        Parser = parser;
        Type = type;
        Attributes = [.. type.GetCustomAttributes().OfType<IShaderAttribute>()];
    }

    public RuntimeReflectionShaderParser Parser { get; }
    Type Type { get; }

    public IShaderType ShaderType => throw new NotImplementedException();

    public string Name => Type.Name;

    public ImmutableArray<IShaderAttribute> Attributes { get; }
}

public sealed class ReflectionShaderStructureTypeSymbol : ISymbol, IStructureSymbol
{
    public ReflectionShaderStructureTypeSymbol(RuntimeReflectionShaderParser parser, Type type)
    {
        Parser = parser;
        Type = type;
        Attributes = [.. type.GetCustomAttributes().OfType<IShaderAttribute>()];
        _lazyMembers = new(GetMembers);
    }


    public IShaderType ShaderType => throw new NotImplementedException();

    public string Name => Type.Name;

    public ImmutableArray<IShaderAttribute> Attributes { get; }

    public RuntimeReflectionShaderParser Parser { get; }
    public Type Type { get; }

    private Lazy<ImmutableArray<IMemberSymbol>> _lazyMembers;
    ImmutableArray<IMemberSymbol> GetMembers()
    {
        var members = new List<IMemberSymbol>();
        foreach (var field in Type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            members.Add(new ReflectionShaderFieldMemberSymbol(Parser, field));
        }
        return members.ToImmutableArray();
    }
    public ImmutableArray<IMemberSymbol> Members => _lazyMembers.Value;
}
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;
using System.Reflection;

namespace DualDrill.CLSL.Frontend.ReflectionShader;

internal sealed class ReflectionShaderFieldMemberSymbol(
    RuntimeReflectionShaderParser Parser,
    FieldInfo FieldInfo
) : IMemberSymbol
{
    private readonly Lazy<IShaderType> _lazyType = new(() => Parser.ParseType(FieldInfo.FieldType));
    public IShaderType Type => _lazyType.Value;

    public string Name => FieldInfo.Name;

    public ImmutableArray<IShaderAttribute> Attributes { get; } = [.. FieldInfo.GetCustomAttributes().OfType<IShaderAttribute>()];

    IShaderType IMemberSymbol.Type => throw new NotImplementedException();
}
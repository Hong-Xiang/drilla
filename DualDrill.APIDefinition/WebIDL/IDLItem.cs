using DualDrill.ApiGen.DrillLang.Declaration;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DualDrill.ApiGen.WebIDL;


[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(InterfaceMixinDecl), typeDiscriminator: "interface mixin")]
[JsonDerivedType(typeof(InterfaceDecl), typeDiscriminator: "interface")]
[JsonDerivedType(typeof(DictionaryDeclaration), typeDiscriminator: "dictionary")]
[JsonDerivedType(typeof(IncludeDecl), typeDiscriminator: "includes")]
[JsonDerivedType(typeof(EnumDecl), typeDiscriminator: "enum")]
[JsonDerivedType(typeof(TypeDefDecl), typeDiscriminator: "typedef")]
[JsonDerivedType(typeof(NamespaceDecl), typeDiscriminator: "namespace")]
[JsonDerivedType(typeof(FailedToParse), typeDiscriminator: "<failed-to-parse>")]
public interface IDeclaration
{
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(FieldDecl), typeDiscriminator: "field")]
[JsonDerivedType(typeof(AttributeDecl), typeDiscriminator: "attribute")]
[JsonDerivedType(typeof(OperationDecl), typeDiscriminator: "operation")]
[JsonDerivedType(typeof(ConstDecl), typeDiscriminator: "const")]
[JsonDerivedType(typeof(ConstructorDecl), typeDiscriminator: "constructor")]
[JsonDerivedType(typeof(SetlikeMember), typeDiscriminator: "setlike")]
public interface IMember
{
    string? Name { get; }
}

public interface IValue
{
    string Type { get; }
}

public interface IWebIDLMemberContainer
{
    public string Name { get; }
    public IEnumerable<IMember> GetMemebers();
}

public sealed record class ArgumentDecl(
    string Name,
    JsonElement IdlType,
    ConstValue Default
)
{

}

public sealed record class OperationDecl(
    string Name,
    JsonElement IdlType,
    ImmutableArray<ArgumentDecl> Arguments
) : IMember
{
}

public sealed record class ConstructorDecl(
    string Name,
    ImmutableArray<ArgumentDecl> Arguments
) : IMember
{
}

public sealed record class IncludeDecl(
    string Target,
    string Includes
) : IDeclaration
{
}

public sealed record class FailedToParse(
    string? Name,
    string? Content,
    string Exception
) : IDeclaration
{
}

public sealed record class ConstValue(
    string Type,
    JsonElement? Value
) : IValue
{
}



public sealed record class SetlikeMember
(
    ImmutableArray<JsonElement> IdlType
) : IMember
{
    public string? Name => null;
}

public sealed record class FieldDecl(
    string Name,
    JsonElement IdlType
) : IMember
{
}

public sealed record class AttributeDecl(
    string Name,
    JsonElement IdlType,
    bool Readonly
) : IMember
{
}

public sealed record class InterfaceMixinDecl(
    string Name,
    ImmutableArray<IMember> Members
) : IDeclaration, IWebIDLMemberContainer
{
    public IEnumerable<IMember> GetMemebers() => Members;
}

public sealed record class InterfaceDecl(
    string Name,
    ImmutableArray<IMember> Members
) : IDeclaration, IWebIDLMemberContainer
{
    public IEnumerable<IMember> GetMemebers() => Members;
}

public sealed record class DictionaryDeclaration(
    string Name,
    ImmutableArray<IMember> Members
) : IDeclaration, IWebIDLMemberContainer
{
    public IEnumerable<IMember> GetMemebers() => Members;
}

public sealed record class EnumDecl(
    string Name,
    ImmutableArray<EnumValue> Values
) : IDeclaration
{
}
public sealed record class EnumValue(
    string Type,
    string Value
) : IValue
{
}

public sealed record class TypeDefDecl(
    string Name,
    JsonElement IdlType
) : IDeclaration
{
}

public sealed record class NamespaceDecl(
    string Name,
    ImmutableArray<ConstDecl> Members
) : IDeclaration
{
}

public sealed record class ConstDecl(
    string Name,
    JsonElement IdlType,
    ConstValue Value
) : IMember
{
}



public sealed record WebIDLSpec(
    ImmutableArray<IDeclaration> Declarations
)
{
    public int DeclarationCount => Declarations.Length;

    public static WebIDLSpec Parse(JsonDocument doc)
    {
        var parsed = doc.Deserialize<IDeclaration[]>(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return new([.. parsed]);
    }

    public ModuleDeclaration ToModuleDeclaration()
    {
        var parser = new WebIDLSpecParser(WebIDLSpecParser.ParseOption.Default);
        return parser.Parse(this);
    }

    public IEnumerable<IMember> GetAllMembers(IWebIDLMemberContainer decl)
    {
        IEnumerable<IMember> result = decl.GetMemebers();
        var mixins = from d in Declarations.OfType<IncludeDecl>()
                     where d.Target == decl.Name
                     from included in Declarations.OfType<IWebIDLMemberContainer>()
                                                  .Where(v => v.Name == d.Includes)
                     from m in GetAllMembers(included)
                     select m;
        return result.Concat(mixins).OrderBy(m => m.Name);
    }

}

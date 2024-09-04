using DualDrill.ApiGen.DrillLang;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DualDrill.ApiGen.WebIDL;


[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(InterfaceMixin), typeDiscriminator: "interface mixin")]
[JsonDerivedType(typeof(Interface), typeDiscriminator: "interface")]
[JsonDerivedType(typeof(Dictionary), typeDiscriminator: "dictionary")]
[JsonDerivedType(typeof(IncludeDecl), typeDiscriminator: "includes")]
[JsonDerivedType(typeof(WebIDLEnum), typeDiscriminator: "enum")]
[JsonDerivedType(typeof(TypeDef), typeDiscriminator: "typedef")]
[JsonDerivedType(typeof(Namespace), typeDiscriminator: "namespace")]
[JsonDerivedType(typeof(FailedToParse), typeDiscriminator: "<failed-to-parse>")]
public interface IDeclaration
{
}
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Field), typeDiscriminator: "field")]
[JsonDerivedType(typeof(WebIDLAttribute), typeDiscriminator: "attribute")]
[JsonDerivedType(typeof(Operation), typeDiscriminator: "operation")]
[JsonDerivedType(typeof(EnumValue), typeDiscriminator: "const")]
[JsonDerivedType(typeof(Constructor), typeDiscriminator: "constructor")]
[JsonDerivedType(typeof(SetlikeMember), typeDiscriminator: "setlike")]
public interface IMember
{
}

public sealed record class Argument(
    string Name,
    JsonElement IdlType,
    ConstValue Default
)
{

}

public sealed record class Operation(
    string Name,
    JsonElement IdlType,
    ImmutableArray<Argument> Arguments
) : IMember
{
}

public sealed record class Constructor(
    string Name,
    ImmutableArray<Argument> Arguments
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
)
{
}



public sealed record class SetlikeMember
(
    ImmutableArray<JsonElement> IdlType
) : IMember
{
}

public sealed record class Field(
    string Name,
    JsonElement IdlType
) : IMember
{
}

public sealed record class WebIDLAttribute(
    string Name,
    JsonElement IdlType,
    bool Readonly
) : IMember
{
}

public sealed record class InterfaceMixin(
    string Name,
    ImmutableArray<IMember> Members
) : IDeclaration
{
}

public sealed record class Interface(
    string Name,
    ImmutableArray<IMember> Members
) : IDeclaration
{
}

public sealed record class Dictionary(
    string Name,
    ImmutableArray<IMember> Members
) : IDeclaration
{
}

public sealed record class WebIDLEnum(
    string Name,
    ImmutableArray<ConstValue> Values
) : IDeclaration
{
}
public sealed record class TypeDef(
    string Name,
    JsonElement IdlType
) : IDeclaration
{
}

public sealed record class Namespace(
    string Name,
    ImmutableArray<ConstDeclaration> Members
) : IDeclaration
{
}

public sealed record class ConstDeclaration(
    string Name,
    JsonElement IdlType,
    ConstValue Value
)
{
}

public sealed record class EnumValue(
    string Name,
    string Value
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
        return new([.. doc.Deserialize<IDeclaration[]>(new JsonSerializerOptions {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        })]);
        //var results = el.EnumerateArray().Select(n =>
        //{
        //    Debug.Assert(n.GetArrayLength() == 2);
        //    IDeclaration[] parsed = [];
        //    try
        //    {
        //        parsed = n[1].Deserialize<IDeclaration[]>(new JsonSerializerOptions
        //        {
        //            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        //        }) ?? throw new Exception("Deserialize return null exception");
        //    }
        //    catch (Exception e)
        //    {
        //        parsed = [new FailedToParse(
        //                            n[0].GetString(),
        //                            n[1].ToString(),
        //                            e.ToString()
        //                        )];
        //    }
        //    return KeyValuePair.Create(n[0].GetString()!, parsed);
        //});
        //return new(results.SelectMany(x => x.Value).ToImmutableArray());
    }

    public static ITypeReference ParseWebIDLTypeRef(JsonElement doc)
    {
        if (doc.ValueKind == JsonValueKind.String)
        {
            return new PlainTypeRef(doc.GetString()!);
        }
        if (doc.ValueKind == JsonValueKind.Array && doc.GetArrayLength() == 1)
        {
            return ParseWebIDLTypeRef(doc[0]);
        }
        if (doc.ValueKind == JsonValueKind.Object)
        {
            var meta = doc.Deserialize<WebIDLTypeMeta>(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }) ?? throw new JsonException("Failed to parse IDLTypeMeta");
            var isGeneric = !string.IsNullOrEmpty(meta.Generic);
            var t = ParseWebIDLTypeRef(doc.GetProperty("idlType"));
            if (!isGeneric && !meta.Nullable && !meta.Union)
            {
                return t;
            }
            if (!isGeneric && meta.Nullable && !meta.Union)
            {
                return new NullableTypeRef(t);
            }
            if (isGeneric && !meta.Nullable && !meta.Union)
            {
                if (meta.Generic == "Promise")
                {
                    return new FutureTypeRef(t);
                }
                if (meta.Generic == "sequence")
                {
                    return new SequenceTypeRef(t);
                }
            }
        }

        return new UnknownTypeRef(doc);
    }
}

internal sealed record class WebIDLTypeMeta(
    string Generic,
    bool Nullable,
    bool Union,
    string Type
)
{
}


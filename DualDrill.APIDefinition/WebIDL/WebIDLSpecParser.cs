using DualDrill.ApiGen.DrillLang.Declaration;
using DualDrill.ApiGen.DrillLang.Types;
using System.Collections.Immutable;
using System.Text.Json;

namespace DualDrill.ApiGen.WebIDL;

/// <summary>
/// Lowering WebIDL's type system to DrillLang's type system by:
/// * resolving all includes and copy all included members into target types
/// * merge namespaces const declarations and enum declarations into enums
/// </summary>
internal sealed record class WebIDLSpecParser(WebIDLSpecParser.ParseOption Option)
{
    public sealed record class ParseOption(bool FallbackUnionByPickAny)
    {
        public static ParseOption Default = new(true);
    }

    public ModuleDeclaration Parse(WebIDLSpec spec)
    {
        return new ModuleDeclaration(nameof(WebIDLSpec),
            [.. spec.Declarations.OfType<InterfaceDecl>().Select(d => ParseHandleDecl(spec, d)).OfType<HandleDeclaration>()],
            [.. spec.Declarations.OfType<DictionaryDeclaration>()
                                 .Select(d => ParseDictionaryDeclaration(spec, d))],
            [.. ParseEnums(spec)],
            []
        );
    }


    ImmutableHashSet<EnumDeclaration> ParseEnums(WebIDLSpec spec)
    {
        var flagEnums = spec.Declarations.OfType<NamespaceDecl>()
                                     .Select(n =>
                                     {
                                         var values = n.Members.Select(m => new EnumMemberDeclaration(m.Name));
                                         return new EnumDeclaration(n.Name, [.. values], true);
                                     });
        var enums = spec.Declarations.OfType<EnumDecl>()
                                     .Select(n =>
                                     {
                                         var values = n.Values.Select(m => new EnumMemberDeclaration(m.Value));
                                         return new EnumDeclaration(
                                             n.Name,
                                             [.. values], false);
                                     });

        return [.. flagEnums, .. enums];
    }

    public HandleDeclaration? ParseHandleDecl(WebIDLSpec spec, InterfaceDecl decl)
    {
        var members = spec.GetAllMembers(decl).ToImmutableArray();
        var methods = members.OfType<OperationDecl>()
                             .Select(ParseOperationDecl)
                             .OfType<MethodDeclaration>()
                             .OrderBy(m => m.Name);
        var props = members.OfType<AttributeDecl>()
                             .Select(ParseAttribute)
                             .OfType<PropertyDeclaration>()
                             .OrderBy(m => m.Name);
        return new(decl.Name, [.. methods], [.. props]);
    }

    public StructDeclaration ParseDictionaryDeclaration(WebIDLSpec spec, DictionaryDeclaration decl)
    {
        var members = spec.GetAllMembers(decl).ToImmutableArray();
        var fields = members.OfType<FieldDecl>()
                             .Select(ParseFieldDeclaration)
                             .OfType<PropertyDeclaration>()
                             .OrderBy(m => m.Name);
        return new StructDeclaration(decl.Name, [.. fields]);
    }

    public MethodDeclaration? ParseOperationDecl(OperationDecl decl)
    {
        var parameters = decl.Arguments.Select(ParseParameter);
        return new MethodDeclaration(
            decl.Name,
            [.. parameters],
            ParseWebIDLType(decl.IdlType));
    }

    public ParameterDeclaration ParseParameter(ArgumentDecl decl)
    {
        return new ParameterDeclaration(decl.Name, ParseWebIDLType(decl.IdlType), null);
    }

    public PropertyDeclaration? ParseAttribute(AttributeDecl decl)
    {
        return new PropertyDeclaration(decl.Name, ParseWebIDLType(decl.IdlType), false);
    }

    public PropertyDeclaration? ParseFieldDeclaration(FieldDecl decl)
    {
        return new PropertyDeclaration(decl.Name, ParseWebIDLType(decl.IdlType), true);
    }

    ITypeReference ParseWebIDLType(JsonElement doc)
    {
        if (doc.ValueKind == JsonValueKind.String)
        {
            return new OpaqueTypeReference(doc.GetString()!);
        }
        if (doc.ValueKind == JsonValueKind.Array && doc.GetArrayLength() == 1)
        {
            return ParseWebIDLType(doc[0]);
        }
        var generic = doc.GetProperty("generic").Deserialize<string?>();
        var isNullable = doc.GetProperty("nullable").Deserialize<bool?>() ?? false;
        var isUnion = doc.GetProperty("union").Deserialize<bool?>() ?? false;
        var isGeneric = !string.IsNullOrEmpty(generic);
        var tDoc = doc.GetProperty("idlType");
        ITypeReference? t = null;
        if (isUnion)
        {
            var unions = tDoc.EnumerateArray()
                             .Select(ParseWebIDLType)
                             .ToImmutableArray();
            if (unions.Length == 2 && unions.Count(t => t is VoidTypeReference) == 1)
            {
                t = unions.Single(t => t is not VoidTypeReference);
            }
            else
            {
                if (Option.FallbackUnionByPickAny)
                {
                    t = unions[0];
                }
                else
                {
                    throw new NotSupportedException("WebIDL union type is not supported");
                }
            }
        }
        if (isGeneric)
        {
            if (generic == "Promise")
            {
                t = new FutureTypeReference(ParseWebIDLType(tDoc));
            }
            if (generic == "sequence")
            {
                t = new SequenceTypeReference(ParseWebIDLType(tDoc));
            }
            if (generic == "record")
            {
                t = new RecordTypeReference(
                    ParseWebIDLType(tDoc[0]),
                    ParseWebIDLType(tDoc[1])
                );
            }
        }
        t ??= ParseWebIDLType(tDoc);
        if (isNullable)
        {
            if (t is null)
            {
                throw new Exception("Failed to parse webidl type");
            }
            t = new NullableTypeReference(t);
        }
        if (t is not null)
        {
            return t;
        }
        return t ?? new UnknownTypeReference(doc);
    }
}


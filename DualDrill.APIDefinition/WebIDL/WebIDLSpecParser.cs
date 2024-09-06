using DualDrill.ApiGen.DrillLang.Declaration;
using DualDrill.ApiGen.DrillLang.Types;
using DualDrill.ApiGen.DrillLang.Value;
using System.Collections.Immutable;
using System.Text.Json;

namespace DualDrill.ApiGen.WebIDL;

internal sealed class WebIDLSpecParser()
{
    public ModuleDeclaration Parse(WebIDLSpec spec)
    {
        return ModuleDeclaration.Create(nameof(WebIDLSpec),
            [
            .. spec.Declarations.OfType<InterfaceDecl>().Select(d => ParseHandleDecl(spec, d)).OfType<HandleDeclaration>(),
            ..ParseEnums(spec) ]);
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

    ITypeReference ParseWebIDLType(JsonElement doc)
    {
        if (doc.ValueKind == JsonValueKind.String)
        {
            return new OpaqueTypeReference(doc.GetString() ?? throw new JsonException("failed to get type name string"));
        }
        if (doc.ValueKind == JsonValueKind.Array && doc.GetArrayLength() == 1)
        {
            return ParseWebIDLType(doc[0]);
        }
        if (doc.ValueKind == JsonValueKind.Object)
        {
            var generic = doc.GetProperty("generic").Deserialize<string?>();
            var isNullable = doc.GetProperty("nullable").Deserialize<bool?>() ?? false;
            var isUnion = doc.GetProperty("union").Deserialize<bool?>() ?? false;
            var isGeneric = !string.IsNullOrEmpty(generic);
            var t = ParseWebIDLType(doc.GetProperty("idlType"));
            if (!isGeneric && !isNullable && !isUnion)
            {
                return t;
            }
            if (!isGeneric && isNullable && !isUnion)
            {
                return new NullableTypeReference(t);
            }
            if (isGeneric && !isNullable && !isUnion)
            {
                if (generic == "Promise")
                {
                    return new FutureTypeReference(t);
                }
                if (generic == "sequence")
                {
                    return new SequenceTypeReference(t);
                }
            }
        }

        return new UnknownTypeReference(doc);
    }
}


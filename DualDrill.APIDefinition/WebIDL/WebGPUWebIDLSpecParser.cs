using DualDrill.ApiGen.DrillLang.Declaration;
using DualDrill.ApiGen.DrillLang.Types;
using DualDrill.ApiGen.DrillLang.Value;
using System.Collections.Immutable;
using System.Text.Json;

namespace DualDrill.ApiGen.WebIDL;

internal sealed class WebGPUWebIDLSpecParser()
{
    public ModuleDeclaration Parse(WebIDLSpec spec)
    {
        return ModuleDeclaration.Create(nameof(WebIDLSpec),
            [
            .. spec.Declarations.OfType<InterfaceDecl>().Select(d => ParseHandleDecl(spec, d)).OfType<HandleDeclaration>(),
            ..ParseEnums(spec) ]);
    }

    string? GetHandleName(string name)
    {
        return name switch
        {
            "GPU" => "GPUInstance",
            "GPUCanvasContext" => "GPUSurface",
            "GPUError" => null,
            "GPUExternalTexture" => null,
            "GPUMapModeFlags" => "GPUMapMode",
            //_ when HandleNames.Contains(name) => name,
            _ => null
        };
    }

    static string SpecName(string idlName)
    {
        return idlName switch
        {
            "GPU" => "GPUInstance",
            "GPUCanvasContext" => "GPUSurface",
            "GPUMapModeFlags" => "GPUMapMode",
            "GPUColorWrite" => "GPUColorWriteMask",
            _ => idlName
        };
    }

    bool IsSupportedEnum(string enumName)
    {
        return enumName switch
        {
            "GPUAutoLayoutMode" => false,
            "GPUPipelineErrorReason" => false,
            "GPUCanvasToneMappingMode" => false,
            "GPUCanvasAlphaMode" => false,
            _ => true
        };
    }

    bool IsSupportedEnumValue(string enumName, string valueName)
    {
        return (enumName, valueName) switch
        {
            ("GPUBlendFactor", _) when valueName.Contains("src1") => false,
            ("GPUVertexFormat", "unorm10-10-10-2") => false,
            ("GPUFeatureName", "texture-compression-bc-sliced-3d") => false,
            ("GPUFeatureName", "clip-distances") => false,
            ("GPUFeatureName", "dual-source-blending") => false,
            _ => true
        };
    }

    ImmutableHashSet<EnumDeclaration> ParseEnums(WebIDLSpec spec)
    {
        var flagEnums = spec.Declarations.OfType<NamespaceDecl>()
                                     .Where(n => IsSupportedEnum(n.Name))
                                     .Select(n =>
                                     {
                                         var values = n.Members.Where(m => IsSupportedEnumValue(n.Name, m.Name))
                                                               .Select(m => new EnumMemberDeclaration(m.Name, new IntegerValue(0)));
                                         return new EnumDeclaration(SpecName(n.Name), [
                                             new EnumMemberDeclaration("none", new IntegerValue(0)),
                                             .. values], true);
                                     });
        var enums = spec.Declarations.OfType<EnumDecl>()
                                     .Where(n => IsSupportedEnum(n.Name))
                                     .Select(n =>
                                     {
                                         var values = n.Values
                                                       .Where(v => IsSupportedEnumValue(n.Name, v.Value))
                                                       .Select(m => new EnumMemberDeclaration(m.Value, null));
                                         if (n.Name == "GPUVertexStepMode")
                                         {
                                             values = values.Append(new EnumMemberDeclaration("VertexBufferNotUsed", null));
                                         }

                                         return new EnumDeclaration(SpecName(n.Name), [.. values], false);
                                     });

        return [.. flagEnums, .. enums];
    }

    public HandleDeclaration? ParseHandleDecl(WebIDLSpec spec, InterfaceDecl decl)
    {
        var name = GetHandleName(decl.Name);
        if (name is null)
        {
            return null;
        }
        var members = spec.GetAllMembers(decl).ToImmutableArray();
        var methods = members.OfType<OperationDecl>()
                             .Select(ParseOperationDecl)
                             .OfType<MethodDeclaration>()
                             .OrderBy(m => m.Name);
        var props = members.OfType<AttributeDecl>()
                             .Select(ParseAttribute)
                             .OfType<PropertyDeclaration>()
                             .OrderBy(m => m.Name);
        return new(name, [.. methods], [.. props]);
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
    ITypeReference ParseIdlTypeName(string name)
    {
        return name switch
        {
            "undefined" => new VoidTypeReference(),
            "boolean" => new BoolTypeReference(),
            "USVString" => new StringTypeReference(),
            "GPUSize64" => new IntegerTypeReference(BitWidth.N64, false),
            "GPUSize32" => new IntegerTypeReference(BitWidth.N32, false),
            "GPUIndex16" => new IntegerTypeReference(BitWidth.N16, true),
            "GPUIndex32" => new IntegerTypeReference(BitWidth.N32, true),
            "GPUIndex64" => new IntegerTypeReference(BitWidth.N64, true),
            "GPUBufferDynamicOffset" => new IntegerTypeReference(BitWidth.N32, false),
            "ArrayBuffer" => new SequenceTypeReference(new IntegerTypeReference(BitWidth.N8, false)),
            "Uint32Array" => new SequenceTypeReference(new IntegerTypeReference(BitWidth.N32, false)),
            _ => new OpaqueTypeReference(GetHandleName(name) is string value ? value : name),
        };
    }
}

